(function () {
  try {
    var saved = localStorage.getItem('theme');
    var theme = saved === 'light' || saved === 'dark' ? saved : 'dark';
    if (theme === 'dark') document.documentElement.classList.add('dark');
  } catch (e) {
    document.documentElement.classList.add('dark');
  }
})();
