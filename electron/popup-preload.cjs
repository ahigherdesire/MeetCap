'use strict'
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('meetcapPopup', {
  onMeeting(cb) {
    ipcRenderer.on('popup:meeting', (_e, meeting) => cb(meeting))
  },
  start() {
    ipcRenderer.send('popup:start')
  },
  dismiss() {
    ipcRenderer.send('popup:dismiss')
  },
})
