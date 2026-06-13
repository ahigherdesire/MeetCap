'use strict'
const { exec } = require('child_process')

/**
 * System-wide meeting detection for the desktop app.
 *
 * Unlike a sandboxed web page, the Electron main process can enumerate OS
 * window titles, so it can tell when a Zoom or Google Meet call is actually in
 * session — across any browser tab or the native Zoom client — and fire a popup
 * at the *start* of the meeting.
 *
 * This is read-only observation of window titles. It does not inject into,
 * scrape, or bypass Zoom, Meet, the browser, or the OS.
 */

const POLL_MS = 3000

// Titles like "Meet - abc-defg-hij - Google Chrome" / "Zoom Meeting".
const MEET_RE = /(^|[\s\-–|])meet(\s*[-–]\s*|\.google\.com)/i
const MEET_BROWSER_RE = /(chrome|edge|firefox|brave|chromium|opera|vivaldi|arc)/i
const ZOOM_MEETING_RE = /zoom meeting/i

function listWindowTitlesWindows() {
  return new Promise((resolve) => {
    const ps =
      'Get-Process | Where-Object { $_.MainWindowTitle } | ' +
      'Select-Object ProcessName, MainWindowTitle | ConvertTo-Json -Compress'
    exec(
      `powershell -NoProfile -ExecutionPolicy Bypass -Command "${ps}"`,
      { windowsHide: true, timeout: POLL_MS - 200 },
      (err, stdout) => {
        if (err || !stdout) return resolve([])
        try {
          let parsed = JSON.parse(stdout)
          if (!Array.isArray(parsed)) parsed = [parsed]
          resolve(
            parsed.map((p) => ({
              process: String(p.ProcessName || ''),
              title: String(p.MainWindowTitle || ''),
            })),
          )
        } catch {
          resolve([])
        }
      },
    )
  })
}

function listWindowTitlesMac() {
  return new Promise((resolve) => {
    const script =
      'tell application "System Events" to get name of (every process whose background only is false)'
    exec(`osascript -e '${script}'`, { timeout: POLL_MS - 200 }, (err, stdout) => {
      if (err || !stdout) return resolve([])
      resolve(stdout.split(',').map((s) => ({ process: s.trim(), title: s.trim() })))
    })
  })
}

function listWindowTitlesLinux() {
  return new Promise((resolve) => {
    exec('wmctrl -l', { timeout: POLL_MS - 200 }, (err, stdout) => {
      if (err || !stdout) return resolve([])
      resolve(
        stdout
          .split('\n')
          .filter(Boolean)
          .map((line) => {
            const title = line.split(/\s+/).slice(3).join(' ')
            return { process: '', title }
          }),
      )
    })
  })
}

function listWindows() {
  if (process.platform === 'win32') return listWindowTitlesWindows()
  if (process.platform === 'darwin') return listWindowTitlesMac()
  return listWindowTitlesLinux()
}

/** Returns { platform, title } for the first matching meeting, or null. */
function classify(windows) {
  for (const w of windows) {
    const proc = (w.process || '').toLowerCase()
    const title = w.title || ''
    // Native Zoom client shows a "Zoom Meeting" window only while in a call.
    if (proc.includes('zoom') && ZOOM_MEETING_RE.test(title)) {
      return { platform: 'zoom', title: 'Zoom meeting' }
    }
    if (ZOOM_MEETING_RE.test(title)) {
      return { platform: 'zoom', title: 'Zoom meeting' }
    }
  }
  for (const w of windows) {
    const proc = (w.process || '').toLowerCase()
    const title = w.title || ''
    // Google Meet runs in a browser tab; its title contains "Meet - <code>".
    const looksBrowser = MEET_BROWSER_RE.test(proc) || MEET_BROWSER_RE.test(title)
    if (looksBrowser && MEET_RE.test(title)) {
      const m = title.match(/meet\s*[-–]\s*([a-z0-9-]+)/i)
      return { platform: 'google-meet', title: m ? `Google Meet (${m[1]})` : 'Google Meet call' }
    }
  }
  return null
}

/**
 * Polls for meetings and emits lifecycle events. Debounces so a single call
 * only fires "started" once, and only fires "ended" after it truly disappears.
 */
function createDetector({ onStarted, onEnded } = {}) {
  let timer = null
  let activeKey = null
  let missCount = 0
  let paused = false

  async function tick() {
    if (paused) return
    let windows = []
    try {
      windows = await listWindows()
    } catch {
      windows = []
    }
    const found = classify(windows)

    if (found) {
      missCount = 0
      if (activeKey !== found.platform) {
        activeKey = found.platform
        onStarted &&
          onStarted({
            platform: found.platform,
            title: found.title,
            source: 'window-title',
            detectedAt: Date.now(),
          })
      }
    } else if (activeKey) {
      // Require two consecutive misses to avoid flicker between polls.
      missCount += 1
      if (missCount >= 2) {
        const ended = activeKey
        activeKey = null
        missCount = 0
        onEnded && onEnded({ platform: ended })
      }
    }
  }

  return {
    start() {
      if (timer) return
      tick()
      timer = setInterval(tick, POLL_MS)
    },
    stop() {
      if (timer) clearInterval(timer)
      timer = null
    },
    setPaused(v) {
      paused = !!v
    },
    reset() {
      activeKey = null
      missCount = 0
    },
  }
}

module.exports = { createDetector, classify, POLL_MS }
