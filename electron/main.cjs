'use strict'
const {
  app,
  BrowserWindow,
  Tray,
  Menu,
  ipcMain,
  nativeImage,
  screen,
  session,
  desktopCapturer,
  shell,
} = require('electron')
const path = require('path')
const { createDetector } = require('./detection.cjs')

const isDev = !app.isPackaged
const DEV_URL = process.env.MEETCAP_URL || 'http://localhost:5180'
const ICON = path.join(__dirname, 'icon.png')

let mainWindow = null
let popupWindow = null
let tray = null
let detector = null
let recordingState = 'idle'
let detectionEnabled = true
let dismissedPlatform = null // suppress re-popping for a meeting the user declined
let quitting = false

// Single-instance: a second launch just focuses the running app.
if (!app.requestSingleInstanceLock()) {
  app.quit()
} else {
  app.on('second-instance', () => showMain())
  app.whenReady().then(init)
}

function init() {
  configureSession()
  createMainWindow()
  createTray()
  startDetector()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createMainWindow()
    else showMain()
  })
}

// ---- Sessions / permissions -------------------------------------------------

function configureSession() {
  const ses = session.defaultSession

  // Provide a screen source so getDisplayMedia() works inside Electron.
  // On Windows/Linux we also attach loopback (system) audio — something a
  // sandboxed browser tab cannot do. Consent is still gated by our own UI.
  ses.setDisplayMediaRequestHandler(
    (_request, callback) => {
      desktopCapturer.getSources({ types: ['screen', 'window'] }).then((sources) => {
        const screenSrc = sources.find((s) => s.id.startsWith('screen')) || sources[0]
        if (!screenSrc) return callback({})
        const audio = process.platform === 'darwin' ? undefined : 'loopback'
        callback({ video: screenSrc, audio })
      })
    },
    { useSystemPicker: false },
  )

  ses.setPermissionRequestHandler((_wc, permission, cb) => {
    cb(['media', 'display-capture', 'audioCapture', 'videoCapture', 'clipboard-read'].includes(permission))
  })
}

// ---- Windows ----------------------------------------------------------------

function loadRenderer(win, hash = '') {
  if (isDev) win.loadURL(DEV_URL + hash)
  else win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'), hash ? { hash } : undefined)
}

function createMainWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 820,
    minWidth: 880,
    minHeight: 640,
    show: false,
    backgroundColor: '#0f172a',
    icon: ICON,
    title: 'MeetCap',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  loadRenderer(mainWindow)
  mainWindow.once('ready-to-show', () => mainWindow.show())

  // Closing the window hides to tray instead of quitting (background app).
  mainWindow.on('close', (e) => {
    if (!quitting) {
      e.preventDefault()
      mainWindow.hide()
    }
  })

  // Open external links in the user's browser, never in-app.
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url)
    return { action: 'deny' }
  })
}

function showMain() {
  if (!mainWindow || mainWindow.isDestroyed()) createMainWindow()
  if (mainWindow.isMinimized()) mainWindow.restore()
  mainWindow.show()
  mainWindow.focus()
}

function createPopup(meeting) {
  closePopup()
  const display = screen.getPrimaryDisplay()
  const { width, height, x, y } = display.workArea
  const w = 376
  const h = 250
  popupWindow = new BrowserWindow({
    width: w,
    height: h,
    x: x + width - w - 16,
    y: y + height - h - 16,
    frame: false,
    transparent: true,
    resizable: false,
    skipTaskbar: true,
    alwaysOnTop: true,
    show: false,
    focusable: true,
    webPreferences: {
      preload: path.join(__dirname, 'popup-preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })
  popupWindow.setAlwaysOnTop(true, 'screen-saver')
  popupWindow.loadFile(path.join(__dirname, 'popup.html'))
  popupWindow.once('ready-to-show', () => {
    popupWindow.showInactive()
    popupWindow.webContents.send('popup:meeting', meeting)
  })
  popupWindow.on('closed', () => {
    popupWindow = null
  })
}

function closePopup() {
  if (popupWindow && !popupWindow.isDestroyed()) popupWindow.close()
  popupWindow = null
}

// ---- Tray -------------------------------------------------------------------

function createTray() {
  const img = nativeImage.createFromPath(ICON).resize({ width: 18, height: 18 })
  tray = new Tray(img)
  tray.setToolTip('MeetCap — consent-first recorder')
  refreshTrayMenu()
  tray.on('click', showMain)
}

function refreshTrayMenu() {
  if (!tray) return
  const statusLabel =
    recordingState === 'recording'
      ? '● Recording'
      : recordingState === 'paused'
        ? '❚❚ Paused'
        : 'Idle'
  const menu = Menu.buildFromTemplate([
    { label: `MeetCap — ${statusLabel}`, enabled: false },
    { type: 'separator' },
    { label: 'Open MeetCap', click: showMain },
    {
      label: 'Background detection',
      type: 'checkbox',
      checked: detectionEnabled,
      click: (item) => setDetectionEnabled(item.checked),
    },
    { type: 'separator' },
    {
      label: 'Quit MeetCap',
      click: () => {
        quitting = true
        app.quit()
      },
    },
  ])
  tray.setContextMenu(menu)
}

// ---- Detection --------------------------------------------------------------

function startDetector() {
  detector = createDetector({
    onStarted: (meeting) => {
      // Tell the renderer regardless (keeps in-app state aware).
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('desktop:meeting-detected', meeting)
      }
      // Don't interrupt an active recording, and respect a prior dismissal.
      if (recordingState !== 'idle') return
      if (dismissedPlatform === meeting.platform) return
      createPopup(meeting)
      pendingMeeting = meeting
    },
    onEnded: ({ platform }) => {
      if (dismissedPlatform === platform) dismissedPlatform = null
      // If the popup is still asking about a meeting that just ended, retire it.
      if (popupWindow && pendingMeeting && pendingMeeting.platform === platform && recordingState === 'idle') {
        closePopup()
        pendingMeeting = null
      }
    },
  })
  detector.start()
}

let pendingMeeting = null

function setDetectionEnabled(enabled) {
  detectionEnabled = enabled
  detector && detector.setPaused(!enabled)
  if (!enabled) {
    closePopup()
    detector && detector.reset()
  }
  refreshTrayMenu()
}

// ---- IPC --------------------------------------------------------------------

ipcMain.on('popup:start', () => {
  closePopup()
  showMain()
  if (pendingMeeting && mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('desktop:start-recording', pendingMeeting)
  }
})

ipcMain.on('popup:dismiss', () => {
  if (pendingMeeting) dismissedPlatform = pendingMeeting.platform
  closePopup()
  pendingMeeting = null
})

ipcMain.on('desktop:recording-state', (_e, state) => {
  recordingState = state
  refreshTrayMenu()
  // Keep the tray icon hint fresh; close any stale popup once recording begins.
  if (state !== 'idle') closePopup()
})

ipcMain.on('desktop:detection-enabled', (_e, enabled) => setDetectionEnabled(enabled))

app.on('before-quit', () => {
  quitting = true
  detector && detector.stop()
})

app.on('window-all-closed', () => {
  // Stay alive in the tray; only quit explicitly.
})
