import React, { Component } from "react"
import { connect } from "react-redux"
import { withRouter } from "react-router"
import { Route, Switch } from "react-router-dom"
import { LOCATION_CHANGE } from "react-router-redux"
import fetch from "isomorphic-fetch"
import marked from "marked"
import highlight from "highlight.js"

import Header from "./header"
import Footer from "./footer"

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

	return wrapInColumns(`<pre><code class="box ${lang}">${code}</code></pre>`);
}
renderer.heading = (text, level) => `<h${level} id="${toSeoFriendly(text)}" class="title is-${level}"><span>${htmlEncode(text)}</span></h${level}>`;
renderer.link = (href, title, text) => `<a href="${href}" target="${/^[\/#].*/.test(href) ? "_self" : "_blank"}">${text}</a>`;
renderer.list = (body, ordered) => {
	return ordered
		? `<div class="content"><ol>${body}</ol></div>` :
		`<div class="content"><ul>${body}</ul></div>`;
};
renderer.paragraph = text => wrapInColumns(`<p>${text}</p>`);
marked.setOptions({
	renderer,
	highlight: (code, language, callback) => {
		//code = code.replace(/</g, "&lt;").replace(/>/g, "&gt;");
		if (language) {
			return highlight.highlight(language, code, true).value;
		}

		return highlight.highlightAuto(code).value;
	}
});

class Layout extends Component {

	state = {
		content: "",
		page: ""
	}

	constructor(props) {
		super(props);

		this.loadPage(this.props.history.location);
		this.props.history.listen(this.loadPage);
	}

	loadPage = (location) => {
		var match = location.pathname.match(/\/CsvHelper\/([^\?#/]+).*/);
		var page = !match ? "home" : match[1];

		fetch(`/CsvHelper/pages/${page}.md`, {
			method: "get",
			credentials: "same-origin",
			headers: {
				"Content-Type": "text/plain"
			}
		}).then(response => response.text()).then(content => {
			this.setState({ content, page }, this.scrollToHash);
		});
	}

	scrollToHash = () => {
		if (!this.props.location.hash) {
			window.scroll(0, 0);
			return;
		}

		const hash = this.props.location.hash.substr(1);
		const element = document.getElementById(hash);
		if (element) {
			element.scrollIntoView(true);
		}
	}

	render() {
		const { content, page } = this.state;
		const markdown = marked(content);

		return (
			<div className="layout">
				<a className="fork-me-on-github" href="https://github.com/joshclose/csvhelper" target="_blank">
					<img src="https://s3.amazonaws.com/github/ribbons/forkme_right_gray_6d6d6d.png" alt="Fork me on GitHub" />
				</a>
				<Header />
				<div id="top-of-page"></div>
				<div className="container">
					<Switch>
						<Route path="/" render={() => <div className={page} dangerouslySetInnerHTML={{ __html: markdown }}></div>} />
					</Switch>
				</div>
				<Footer />
			</div >
		)
	}

}

export default withRouter(connect()(Layout))