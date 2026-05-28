document.body.addEventListener('htmx:syntax:error', function (e) {
  console.error(e.detail.error, e.detail.elt);
  console.error('error:', e.detail.error);
});
