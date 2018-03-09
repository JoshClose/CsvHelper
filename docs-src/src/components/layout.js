import React, { Component } from "react"

import Header from "./header"

export default class Layout extends Component {

	render() {
		return (
			<div className="layout">
				<a className="fork-me-on-github" href="https://github.com/joshclose/csvhelper" target="_blank">
					<img src="https://s3.amazonaws.com/github/ribbons/forkme_right_gray_6d6d6d.png" alt="Fork me on GitHub" />
				</a>
				<Header />
				{this.props.children}
			</div>
		)
	}

}
