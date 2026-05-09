(() => {
  const root = document.documentElement;

  try {
    const saved = localStorage.getItem('theme');
    const theme = saved === 'light' || saved === 'dark' ? saved : 'dark';

    root.classList.toggle('dark', theme === 'dark');
  } catch (error) {
    console.warn('Failed to restore saved theme preference.', error);
    root.classList.add('dark');
  }
})();
