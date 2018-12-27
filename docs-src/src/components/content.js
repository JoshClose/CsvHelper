import React, { Component } from "react";
import { withSiteData, withRouteData } from "react-static";
import marked from "marked";
import highlight from "highlight.js";
import "highlight.js/styles/vs.css";

let isMarkedInitialized = false;

function wrapInColumns(text) {
	return `<div class="columns"><div class="column">${text}</div></div>`;
}

function htmlEncode(text) {
	return text.replace(/</g, "&lt;").replace(/>/g, "&gt");
}

function toSeoFriendly(text) {
	var result = text
		.toLowerCase()
		.match(/[\w]+/g)
		.map(word => encodeURIComponent(word))
		.join("-");
	return result;
}

function initializeMarked(isDev) {
	if (isMarkedInitialized === true) {
		return;
	}

	console.log("initializing marked");

	isMarkedInitialized = true;

	highlight.configure({
		languages: ["cs"]
	});
	const renderer = new marked.Renderer();
	renderer.blockquote = quote => {
		return `<div class="content"><blockquote>${quote}</blockquote></div>`;
	}
	// For some reason using `this` here when this
	// is a lambda function, `this` is undefined,
	// so using a normal function.
	renderer.code = function (code, lang) {
		if (this.options.highlight) {
			code = this.options.highlight(code, lang) || code;
		}

		return wrapInColumns(`<pre><code class="${lang}">${code}</code></pre>`);
	}
	renderer.heading = (text, level) => `<h${level} id="${toSeoFriendly(text)}" class="title is-${level}"><span>${htmlEncode(text)}</span></h${level}>`;
	renderer.link = (href, title, text) => {
		if (!href.startsWith("http") && !isDev) {
			const separator = href.startsWith("/") ? "" : "/";
			href = `/CsvHelper${separator}${href}`;
		}

		return `<a href="${href}" target="${/^[\/#].*/.test(href) ? "_self" : "_self"}">${text}</a>`;
	};
	renderer.list = (body, ordered) => {
		return ordered
			? `<div class="content"><ol>${body}</ol></div>`
			: `<div class="content"><ul>${body}</ul></div>`;
	};
	renderer.paragraph = text => wrapInColumns(`<p>${text}</p>`);
	renderer.table = (header, body) => {
		const replacedHeader = header.replace(/<\/?tr>|<\/?th>|&nbsp;|\s/ig, "");
		const tableClass = replacedHeader.length > 0 ? "" : "has-empty-head";

		return `
			<table class="table ${tableClass}">
				<thead>${header}</thead>
				<tbody>${body}</tbody>
			</table>
		`;
	}

	marked.setOptions({
		renderer,
		highlight: (code, language, callback) => {
			//code = code.replace(/</g, "&lt;").replace(/>/g, "&gt;");
			const h = highlight;
			if (language) {
				return highlight.highlight(language, code, true).value;
			}

			return highlight.highlightAuto(code).value;
		}
	});
}

const Content = ({ className, data, isDev }) => {
	initializeMarked(isDev);

	const markdown = marked(data);

	return (
		<div className={className} dangerouslySetInnerHTML={{ __html: markdown }}></div>
	)
}

export default withSiteData(withRouteData(Content));