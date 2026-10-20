// CsvHelper documentation: theme toggle, navigation drawer, code highlighting, copy buttons, heading anchors, build
// and sample tabs, and the "On this page" scrollspy. A classic script (no modules), so the site also works from file://.
(function () {
	'use strict';

	function storage(key, value) {
		try {
			if (value === undefined) return window.localStorage.getItem(key);
			window.localStorage.setItem(key, value);
		} catch (e) {
			// Storage can be unavailable (private windows, blocked site data); everything works without it.
		}

		return null;
	}

	function template(id) {
		var element = document.getElementById(id);

		return element ? element.innerHTML : '';
	}

	// Theme
	document.addEventListener('click', function (event) {
		if (!event.target.closest('[data-theme-toggle]')) return;
		var dark = document.documentElement.classList.toggle('dark');
		storage('theme', dark ? 'dark' : 'light');
	});

	// Navigation drawer (small screens)
	var sidebar = document.querySelector('[data-sidebar]');
	var backdrop = document.querySelector('[data-sidebar-backdrop]');
	var opener = document.querySelector('[data-sidebar-open]');

	function setSidebar(open) {
		if (!sidebar) return;
		if (open) sidebar.setAttribute('data-open', '');
		else sidebar.removeAttribute('data-open');
		if (backdrop) backdrop.hidden = !open;
		document.body.style.overflow = open ? 'hidden' : '';
		if (open) {
			var link = sidebar.querySelector('[aria-current]') || sidebar.querySelector('a');
			if (link) link.focus();
		} else if (opener) {
			opener.focus();
		}
	}

	if (opener) opener.addEventListener('click', function () { setSidebar(true); });
	document.querySelectorAll('[data-sidebar-close]').forEach(function (button) {
		button.addEventListener('click', function () { setSidebar(false); });
	});
	if (backdrop) backdrop.addEventListener('click', function () { setSidebar(false); });
	document.addEventListener('keydown', function (event) {
		if (event.key === 'Escape' && sidebar && sidebar.hasAttribute('data-open')) setSidebar(false);
	});

	// Keep the current page visible in a long sidebar.
	if (sidebar && window.matchMedia('(min-width: 1024px)').matches) {
		var current = sidebar.querySelector('[aria-current]');
		if (current) {
			var top = current.offsetTop - sidebar.offsetTop;
			if (top > sidebar.clientHeight - 80) sidebar.scrollTop = top - sidebar.clientHeight / 3;
		}
	}

	// Code highlighting (the API signatures are colored by the generator already).
	if (window.hljs) {
		window.hljs.configure({ ignoreUnescapedHTML: true });
		document.querySelectorAll('pre code[class*="language-"]').forEach(function (block) {
			if (!block.classList.contains('language-plaintext')) window.hljs.highlightElement(block);
		});
	}

	// Copy buttons
	function copy(text, button) {
		function done() {
			button.setAttribute('data-copied', '');
			button.innerHTML = template('icon-check');
			setTimeout(function () {
				button.removeAttribute('data-copied');
				button.innerHTML = template('icon-copy');
			}, 1600);
		}

		function fallback() {
			var area = document.createElement('textarea');
			area.value = text;
			area.setAttribute('readonly', '');
			area.style.position = 'fixed';
			area.style.opacity = '0';
			document.body.appendChild(area);
			area.select();
			try {
				document.execCommand('copy');
				done();
			} catch (e) {
				// Nothing else to try.
			}

			document.body.removeChild(area);
		}

		if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).then(done, fallback);
		else fallback();
	}

	function addCopyButton(container, getText, label) {
		var button = document.createElement('button');
		button.type = 'button';
		button.className = 'copy-button';
		button.setAttribute('aria-label', label);
		button.innerHTML = template('icon-copy');
		button.addEventListener('click', function () { copy(getText(), button); });
		container.appendChild(button);
	}

	document.querySelectorAll('pre').forEach(function (pre) {
		var wrapper = document.createElement('div');
		wrapper.className = 'code-block';
		pre.parentNode.insertBefore(wrapper, pre);
		wrapper.appendChild(pre);
		addCopyButton(wrapper, function () { return pre.innerText.replace(/\n$/, ''); }, 'Copy the code');
	});

	document.querySelectorAll('[data-copy-text]').forEach(function (code) {
		var container = code.parentNode;
		container.classList.add('code-block');
		addCopyButton(container, function () { return code.textContent; }, 'Copy the command');
	});

	// Heading anchors
	document.querySelectorAll('.prose-guide h2[id], .prose-guide h3[id], .section-title[id], .api-namespace-title[id]').forEach(function (heading) {
		var anchor = document.createElement('a');
		anchor.className = 'heading-anchor';
		anchor.href = '#' + heading.id;
		anchor.setAttribute('aria-label', 'Link to this section');
		anchor.innerHTML = template('icon-link');
		heading.appendChild(anchor);
	});

	// Build tabs in the API reference. The choice applies to the whole page and is remembered.
	function selectVariant(framework) {
		document.querySelectorAll('[data-variants]').forEach(function (group) {
			var tabs = group.querySelectorAll('[data-variant-tab]');
			var chosen = null;
			tabs.forEach(function (tab) {
				if (!chosen && tab.getAttribute('data-variant-tab').split(' ').indexOf(framework) >= 0) chosen = tab;
			});
			chosen = chosen || tabs[0];
			tabs.forEach(function (tab) {
				var selected = tab === chosen;
				tab.setAttribute('aria-selected', selected ? 'true' : 'false');
				tab.tabIndex = selected ? 0 : -1;
			});
			group.querySelectorAll('[data-variant]').forEach(function (panel) {
				panel.hidden = panel.getAttribute('data-variant') !== chosen.getAttribute('data-variant-tab');
			});
			group.setAttribute('data-ready', '');
		});
	}

	if (document.querySelector('[data-variants]')) {
		selectVariant(storage('framework') || '');
		document.addEventListener('click', function (event) {
			var tab = event.target.closest('[data-variant-tab]');
			if (!tab) return;
			var framework = tab.getAttribute('data-variant-tab').split(' ')[0];
			storage('framework', framework);
			selectVariant(framework);
		});
	}

	// Code sample tabs on the home page
	document.querySelectorAll('[data-samples]').forEach(function (group) {
		var tabs = group.querySelectorAll('[data-sample-tab]');

		function show(tab) {
			tabs.forEach(function (other) {
				var selected = other === tab;
				other.setAttribute('aria-selected', selected ? 'true' : 'false');
				other.tabIndex = selected ? 0 : -1;
			});
			group.querySelectorAll('[data-sample]').forEach(function (panel) {
				panel.hidden = panel.getAttribute('data-sample') !== tab.getAttribute('data-sample-tab');
			});
		}

		tabs.forEach(function (tab, i) {
			tab.addEventListener('click', function () { show(tab); });
			tab.addEventListener('keydown', function (event) {
				var next = event.key === 'ArrowRight' ? tabs[(i + 1) % tabs.length] : event.key === 'ArrowLeft' ? tabs[(i - 1 + tabs.length) % tabs.length] : null;
				if (!next) return;
				event.preventDefault();
				next.focus();
				show(next);
			});
		});
		if (tabs.length) show(tabs[0]);
	});

	// "On this page": highlight the section being read.
	// The current section is the last one whose top has scrolled under the header (or the first one at the top).
	var tocLinks = Array.prototype.slice.call(document.querySelectorAll('[data-toc-link]'));
	if (tocLinks.length) {
		var targets = tocLinks.map(function (link) {
			return document.getElementById(decodeURIComponent(link.getAttribute('href').slice(1)));
		});
		var current = -1;
		var pending = false;

		function update() {
			pending = false;
			var active = 0;
			for (var i = 0; i < targets.length; i++) {
				if (targets[i] && targets[i].getBoundingClientRect().top <= 120) active = i;
			}

			// At the bottom of the page the last sections may never reach the top.
			if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4) active = targets.length - 1;
			if (active === current) return;
			current = active;
			tocLinks.forEach(function (link, k) {
				if (k === active) {
					link.setAttribute('aria-current', 'true');
					var toc = link.closest('.toc');
					if (toc && (link.offsetTop < toc.scrollTop || link.offsetTop > toc.scrollTop + toc.clientHeight - 40)) toc.scrollTop = link.offsetTop - toc.clientHeight / 3;
				} else {
					link.removeAttribute('aria-current');
				}
			});
		}

		window.addEventListener('scroll', function () {
			if (pending) return;
			pending = true;
			window.requestAnimationFrame(update);
		}, { passive: true });
		update();
	}
})();
