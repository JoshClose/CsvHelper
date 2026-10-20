// CsvHelper documentation search: a Ctrl/Cmd+K palette over MiniSearch. The index (search-index.js) and MiniSearch are
// loaded with script tags the first time the palette opens, so search also works from file://.
(function () {
	'use strict';

	var script = document.currentScript;
	var root = script ? script.src.replace(/assets\/search\.js(?:[?#].*)?$/, '') : '';
	var dialog = document.querySelector('[data-search-dialog]');
	var input = document.querySelector('[data-search-input]');
	var results = document.querySelector('[data-search-results]');
	if (!dialog || !input || !results) return;

	var GROUPS = [
		['Guides', ['page', 'guide', 'section']],
		['Types', ['class', 'record', 'struct', 'enum', 'delegate', 'interface', 'attribute', 'exception']],
		['Members', ['constructor', 'property', 'indexer', 'method', 'field', 'value', 'event', 'operator']],
		['Diagnostics', ['diagnostic']],
		['Releases', ['release', 'migration']],
	];
	var BOOST = { class: 2.4, record: 2.4, struct: 2.2, enum: 2, attribute: 2.2, exception: 2, delegate: 1.8, guide: 1.6, page: 1.3, section: 1.1, diagnostic: 1.5, value: 0.8, release: 0.8, migration: 0.8 };
	var SPLIT = /[\s.,;:!?()<>[\]{}"'`=|/\\+*&^%$#@~]+/;

	var index = null;
	var icons = {};
	var waiting = null;
	var items = [];
	var active = -1;
	var opener = null;

	var kbd = document.querySelector('[data-search-kbd]');
	if (kbd && /Mac|iPhone|iPad/.test(navigator.platform || navigator.userAgent)) kbd.textContent = '⌘ K';

	// Words, plus the parts of camelCase names, so "field" finds GetFieldIndex.
	function tokenize(text) {
		var tokens = [];
		String(text || '').split(SPLIT).forEach(function (word) {
			if (!word) return;
			tokens.push(word);
			var parts = word.match(/[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+/g);
			if (parts && parts.length > 1) parts.forEach(function (part) { tokens.push(part); });
		});

		return tokens;
	}

	function queryTokens(text) {
		return String(text || '').split(SPLIT).filter(Boolean);
	}

	function inject(src, onload) {
		var element = document.createElement('script');
		element.src = src;
		element.onload = onload;
		element.onerror = function () {
			results.innerHTML = '<p class="search-empty">The search index could not be loaded.</p>';
			waiting = null;
		};
		document.head.appendChild(element);
	}

	function load(callback) {
		if (index) return callback();
		if (waiting) return waiting.push(callback);
		waiting = [callback];
		inject(root + 'assets/vendor/minisearch.js', function () {
			inject(root + 'search-index.js', function () {
				var data = window.CSVHELPER_SEARCH || { docs: [], icons: {} };
				icons = data.icons;
				index = new window.MiniSearch({
					fields: ['title', 'parent', 'summary', 'text'],
					storeFields: ['title', 'kind', 'url', 'parent', 'summary'],
					tokenize: tokenize,
					processTerm: function (term) { return term.toLowerCase(); },
					searchOptions: {
						tokenize: queryTokens,
						boost: { title: 5, parent: 2, summary: 1.5 },
						prefix: true,
						fuzzy: function (term) { return term.length > 3 ? 0.2 : false; },
						combineWith: 'AND',
						boostDocument: function (id, term, stored) { return (stored && BOOST[stored.kind]) || 1; },
					},
				});
				index.addAll(data.docs);
				var callbacks = waiting;
				waiting = null;
				callbacks.forEach(function (fn) { fn(); });
			});
		});
	}

	function escape(text) {
		return String(text || '').replace(/[&<>"']/g, function (c) {
			return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
		});
	}

	function highlight(text, terms) {
		var html = escape(text);
		terms.slice().sort(function (a, b) { return b.length - a.length; }).forEach(function (term) {
			if (!term) return;
			var pattern = new RegExp('(' + term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'ig');
			html = html.replace(/(<[^>]*>)|([^<]+)/g, function (match, tag, text) {
				return tag || text.replace(pattern, '<mark>$1</mark>');
			});
		});

		return html;
	}

	function render(query) {
		var found;
		var terms = queryTokens(query);
		if (!query.trim()) {
			found = index ? index.search(MiniSearch.wildcard, { filter: function (r) { return r.kind === 'guide' || r.url === 'api/index.html' || r.url === 'changelog.html'; } }).slice(0, 12) : [];
		} else {
			found = index.search(query).slice(0, 60);
		}

		items = [];
		var html = '';
		// Groups in the order of their best result (results come sorted by score), so "field" shows types first.
		var groups = GROUPS.map(function (group) {
			return { title: group[0], matches: found.filter(function (r) { return group[1].indexOf(r.kind) >= 0; }).slice(0, 8) };
		}).filter(function (group) { return group.matches.length; });
		if (query.trim()) {
			groups.sort(function (a, b) { return found.indexOf(a.matches[0]) - found.indexOf(b.matches[0]); });
		}

		groups.forEach(function (group) {
			html += '<p class="search-group" role="presentation">' + group.title + '</p>';
			group.matches.forEach(function (result) {
				var i = items.length;
				items.push(result);
				html += '<a class="search-result" role="option" id="search-result-' + i + '" data-index="' + i + '" aria-selected="false" href="' + escape(root + result.url) + '">' +
					'<span class="search-result-icon">' + (icons[result.kind] || '') + '</span>' +
					'<span class="search-result-body">' +
					'<span class="search-result-title">' + highlight(result.title, terms) + '</span>' +
					(result.parent ? '<span class="search-result-parent">' + escape(result.parent) + '</span>' : '') +
					(result.summary ? '<span class="search-result-summary">' + escape(result.summary) + '</span>' : '') +
					'</span></a>';
			});
		});

		if (!items.length) html = '<p class="search-empty">No results for “' + escape(query) + '”.</p>';
		results.innerHTML = html;
		select(items.length ? 0 : -1);
	}

	function select(i) {
		if (active >= 0) {
			var previous = document.getElementById('search-result-' + active);
			if (previous) previous.setAttribute('aria-selected', 'false');
		}

		active = i;
		if (i < 0) {
			input.removeAttribute('aria-activedescendant');

			return;
		}

		var element = document.getElementById('search-result-' + i);
		element.setAttribute('aria-selected', 'true');
		input.setAttribute('aria-activedescendant', element.id);
		element.scrollIntoView({ block: 'nearest' });
	}

	function go(i) {
		var element = document.getElementById('search-result-' + i);
		if (!element) return;
		close();
		window.location.href = element.href;
	}

	function open() {
		if (dialog.open) return;
		opener = document.activeElement;
		dialog.showModal();
		input.select();
		results.innerHTML = '<p class="search-empty">Loading…</p>';
		load(function () { render(input.value); });
	}

	function close() {
		if (dialog.open) dialog.close();
	}

	dialog.addEventListener('close', function () {
		if (opener && opener.focus) opener.focus();
	});
	dialog.addEventListener('click', function (event) {
		if (event.target === dialog) close();
	});
	input.addEventListener('input', function () {
		if (index) render(input.value);
	});
	input.addEventListener('keydown', function (event) {
		if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
			event.preventDefault();
			if (!items.length) return;
			var step = event.key === 'ArrowDown' ? 1 : -1;
			select((active + step + items.length) % items.length);
		} else if (event.key === 'Enter') {
			event.preventDefault();
			go(active < 0 ? 0 : active);
		}
	});
	results.addEventListener('mousemove', function (event) {
		var item = event.target.closest('[data-index]');
		if (item && Number(item.getAttribute('data-index')) !== active) select(Number(item.getAttribute('data-index')));
	});
	results.addEventListener('click', function (event) {
		var item = event.target.closest('[data-index]');
		if (!item || event.ctrlKey || event.metaKey || event.shiftKey) return;
		event.preventDefault();
		go(Number(item.getAttribute('data-index')));
	});

	document.addEventListener('click', function (event) {
		if (event.target.closest('[data-search-open]')) open();
		if (event.target.closest('[data-search-close]')) close();
	});
	document.addEventListener('keydown', function (event) {
		var typing = event.target.closest && event.target.closest('input, textarea, select, [contenteditable="true"]');
		if ((event.key === 'k' || event.key === 'K') && (event.ctrlKey || event.metaKey)) {
			event.preventDefault();
			if (dialog.open) close();
			else open();
		} else if (event.key === '/' && !typing && !dialog.open) {
			event.preventDefault();
			open();
		}
	});
})();
