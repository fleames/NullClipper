const titleEl = document.getElementById('title');
const detailEl = document.getElementById('detail');
const previewHost = document.getElementById('preview-host');
const preview = document.getElementById('preview');
const gifBadge = document.getElementById('gif-badge');

async function render() {
  const { toast } = await chrome.storage.session.get('toast');
  if (!toast) return;
  titleEl.textContent = toast.title || 'NullClipper';
  detailEl.textContent = toast.detail || '';
  detailEl.classList.toggle('hidden', !toast.detail);
  if (toast.previewDataUrl) {
    preview.src = toast.previewDataUrl;
    previewHost.classList.remove('hidden');
  } else {
    previewHost.classList.add('hidden');
  }
  gifBadge.classList.toggle('on', Boolean(toast.gif && toast.previewDataUrl));
}

render();
chrome.storage.session.onChanged.addListener((changes) => {
  if (changes.toast) render();
});

setTimeout(() => window.close(), 3200);
