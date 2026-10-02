const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('codexUsage', {
  refresh: () => ipcRenderer.invoke('usage:refresh'),
  onUpdate: (callback) => ipcRenderer.on('usage:update', (_event, value) => callback(value)),
  getSettings: () => ipcRenderer.invoke('app:getSettings'),
  setAlwaysOnTop: (value) => ipcRenderer.invoke('window:setAlwaysOnTop', value),
  setOpenAtLogin: (value) => ipcRenderer.invoke('app:setLoginItem', value),
  openUsage: () => ipcRenderer.invoke('app:openUsage')
});
