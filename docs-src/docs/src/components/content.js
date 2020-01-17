import React, { Component, createRef } from "react";
import { withRouter } from "react-router";
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

function initializeMarked(basePath) {
	if (isMarkedInitialized === true) {
		return;
	}

	isMarkedInitialized = true;

	highlight.configure({
		languages: ["cs"]
	});

	const renderer = new marked.Renderer();

	renderer.blockquote = quote => {
		return `<div class="content"><blockquote>${quote}</blockquote></div>`;
	};

	// For some reason using `this` here when this
	// is a lambda function, `this` is undefined,
	// so using a normal function.
	renderer.code = function (code, lang) {
		if (this.options.highlight) {
			code = this.options.highlight(code, lang) || code;
		}

		return `<div class="columns"><div class="column" style="overflow-x: auto"><pre><code class="${lang}">${code}</code></pre></div></div>`;
	};

	renderer.heading = (text, level) => `<h${level} id="${toSeoFriendly(text)}" class="title is-${level}"><span>${text}</span></h${level}>`;

	renderer.link = (href, title, text) => {
		if (basePath) {
			href = `${basePath}${href}`;
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

class Content extends Component {

	constructor(props) {
		super(props);

		this.divRef = createRef();

		initializeMarked(this.props.basePath);
	}

	componentDidMount() {
		this.intersectionObserver = new IntersectionObserver(this.handleDivObserved);
		this.intersectionObserver.observe(this.divRef.current);
	}

	componentDidUpdate(prevProps) {
		if (prevProps.data !== this.props.data) {
			this.removeAnchorEventListeners();
			this.registerAnchorEventListeners();
		}

		if (prevProps.location !== this.props.location) {
			this.scroll();
		}
	}

	componentWillUnmount() {
		this.removeAnchorEventListeners();
	}

	registerAnchorEventListeners() {
		this.anchors = this.divRef.current.getElementsByTagName("a");

		for (const anchor of this.anchors) {
			anchor.addEventListener("click", this.handleAnchorClick);
		}
	}

	removeAnchorEventListeners() {
		for (const anchor of this.anchors) {
			anchor.removeEventListener("click", this.handleAnchorClick);
		}
	}

	scroll() {
		const hash = this.props.location.hash.slice(1);
		if (hash) {
			document.getElementById(hash).scrollIntoView();
		}
		else {
			window.scrollTo(0, 0);
		}
	}

	handleDivObserved = () => {
		this.registerAnchorEventListeners();
		this.scroll();
	}

	handleAnchorClick = (e) => {
		const { history, basePath } = this.props;
		let href = e.currentTarget.getAttribute("href");
		if (href.startsWith(basePath)) {
			href = href.replace(basePath, "");
		}

		if (href[0] === "/") {
			e.preventDefault();
			history.push(href);
		}
	}

	render() {
		const { className, data } = this.props;

		const markdown = marked(data);

		return (
			<div ref={this.divRef} className={className} dangerouslySetInnerHTML={{ __html: markdown }}></div>
		);
	}
}

export default withSiteData(withRouteData(withRouter(Content)))
