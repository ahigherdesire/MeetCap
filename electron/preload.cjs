'use strict'
const { contextBridge, ipcRenderer } = require('electron')

// Bridge exposed to the React renderer. Mirrors the `desktopAdapter` contract
// in src/lib/detection.ts so the web UI lights up "desktop detection: available".
contextBridge.exposeInMainWorld('meetcapDesktop', {
  isDesktop: true,
  platform: process.platform,

  /** Main asks the renderer to begin recording a detected meeting. */
  onStartRecording(cb) {
    const handler = (_e, meeting) => cb(meeting)
    ipcRenderer.on('desktop:start-recording', handler)
    return () => ipcRenderer.removeListener('desktop:start-recording', handler)
  },

  /** Optional: surface raw detection events to the renderer. */
  onMeetingDetected(cb) {
    const handler = (_e, meeting) => cb(meeting)
    ipcRenderer.on('desktop:meeting-detected', handler)
    return () => ipcRenderer.removeListener('desktop:meeting-detected', handler)
  },

  /** Renderer reports recording state so the tray + popup stay in sync. */
  setRecordingState(state) {
    ipcRenderer.send('desktop:recording-state', state)
  },

  /** Toggle background detection from Settings. */
  setDetectionEnabled(enabled) {
    ipcRenderer.send('desktop:detection-enabled', enabled)
  },
})
