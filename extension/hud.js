const timer = document.getElementById('timer');
const port = chrome.runtime.connect({ name: 'nc-hud' });

document.getElementById('stop').addEventListener('click', () => {
  chrome.runtime.sendMessage({ type: 'NC_STOP_GIF' });
});

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') {
    chrome.runtime.sendMessage({ type: 'NC_STOP_GIF' });
  }
});

chrome.runtime.onMessage.addListener((message) => {
  if (message?.type === 'NC_GIF_TICK' && message.label) {
    timer.textContent = message.label;
  }
});

window.addEventListener('beforeunload', () => {
  port.disconnect();
});
