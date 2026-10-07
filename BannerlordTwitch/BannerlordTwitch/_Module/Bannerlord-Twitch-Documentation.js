/* Progressive enhancement: the complete guide remains readable without JavaScript. */
(() => {
  'use strict';
  const tutorial = document.getElementById('tutorial');
  const launchTutorial = document.getElementById('tutorial-launch');
  if (tutorial && typeof tutorial.showModal === 'function') {
    launchTutorial.hidden = false;
    launchTutorial.addEventListener('click', () => tutorial.showModal());
    document.getElementById('tutorial-close').addEventListener('click', () => tutorial.close());
    document.getElementById('tutorial-done').addEventListener('click', () => tutorial.close());
    tutorial.addEventListener('click', event => {
      const bounds = tutorial.getBoundingClientRect();
      if (event.target === tutorial && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) tutorial.close();
    });
  }
  const entries = [];
  document.querySelectorAll('.commands, .rewards').forEach(section => {
    const table = section.querySelector('table');
    if (!table) return;
    const wrapper = table.parentElement;
    const list = document.createElement('div');
    list.className = 'entry-list';
    const isCommand = section.classList.contains('commands');
    Array.from(table.rows).filter(row => row.parentElement.closest('table') === table && row.cells.length === 3 && row.cells[0].tagName === 'TD').forEach(row => {
      const [name, description, settings] = Array.from(row.cells);
      const entry = document.createElement('article');
      entry.className = 'guide-entry';
      const overview = document.createElement('div');
      overview.className = 'entry-overview';
      const heading = document.createElement('h2');
      const command = isCommand ? '!' + name.textContent.trim().replace(/^!/, '') : name.textContent.trim();
      heading.textContent = command;
      const body = document.createElement('div');
      body.className = 'entry-description';
      body.append(...description.childNodes);
      overview.append(heading, body);
      if (isCommand) {
        const copy = document.createElement('button');
        copy.className = 'copy-command';
        copy.type = 'button';
        copy.textContent = 'Copy';
        copy.setAttribute('aria-label', 'Copy ' + command);
        copy.addEventListener('click', async () => {
          try {
            await navigator.clipboard.writeText(command);
            copy.textContent = 'Copied';
            document.getElementById('search-status').textContent = command + ' copied. Check its details for required arguments.';
          } catch (_) {
            // Browsers without clipboard access still offer selectable command text.
            const range = document.createRange();
            range.selectNodeContents(heading);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            document.getElementById('search-status').textContent = 'Select ' + command + ' and copy it manually.';
          }
          window.setTimeout(() => { copy.textContent = 'Copy'; }, 1800);
        });
        overview.append(copy);
      }
      entry.append(overview);
      if (settings.textContent.trim()) {
        const details = document.createElement('details');
        const summary = document.createElement('summary');
        summary.textContent = 'Details and settings';
        const inner = document.createElement('div');
        inner.className = 'entry-settings';
        inner.append(...settings.childNodes);
        details.append(summary, inner);
        entry.append(details);
      }
      list.append(entry);
      entries.push({ element: entry, text: entry.textContent.toLocaleLowerCase(), section });
    });
    if (wrapper.tagName === 'DETAILS') wrapper.replaceWith(list);
    else table.replaceWith(list);
    if (!list.children.length) {
      const empty = document.createElement('p');
      empty.textContent = isCommand ? 'No chat commands are enabled for this stream.' : 'No Channel Point Rewards are enabled for this stream.';
      list.append(empty);
    }
  });
  const input = document.getElementById('guide-search');
  const status = document.getElementById('search-status');
  function filter() {
    const words = input.value.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
    let count = 0;
    entries.forEach(entry => {
      entry.element.hidden = !words.every(word => entry.text.includes(word));
      if (!entry.element.hidden) count++;
    });
    status.textContent = count + (count === 1 ? ' command or reward' : ' commands and rewards') + (words.length ? ' found' : ' available');
    document.getElementById('no-results').hidden = count > 0 || !words.length;
    // Search only filters the command/reward list; campaign reference material stays available.
  }
  document.querySelector('.search-tools').hidden = false;
  input.addEventListener('input', filter);
  document.getElementById('clear-search').addEventListener('click', () => { input.value = ''; filter(); input.focus(); });
  document.querySelectorAll('.toc-container a').forEach(link => {
    link.addEventListener('click', () => {
      input.value = ''; filter();
      const target = document.getElementById(link.hash.slice(1));
      for (let parent = target?.parentElement; parent; parent = parent.parentElement) {
        if (parent.tagName === 'DETAILS') parent.open = true;
      }
    });
  });
  const revealHash = () => {
    const target = document.getElementById(location.hash.slice(1));
    const links = Array.from(document.querySelectorAll('.toc-container a'));
    links.forEach((link, index) => {
      if (link.hash === location.hash || (!location.hash && index === 0)) link.setAttribute('aria-current', 'location');
      else link.removeAttribute('aria-current');
    });
    for (let parent = target?.parentElement; parent; parent = parent.parentElement) {
      if (parent.tagName === 'DETAILS') parent.open = true;
    }
  };
  window.addEventListener('hashchange', revealHash);
  revealHash();
  filter();
})();
