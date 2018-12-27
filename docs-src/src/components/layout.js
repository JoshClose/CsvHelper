import React, { Component, Fragment } from "react"

import Header from "./header"
import Footer from "./footer";

export default class Layout extends Component {

	render() {
		return (
			<Fragment>
				<Header />
				<div className="body">
					{this.props.children}
				</div>
				<Footer />
			</Fragment>
		)
	}

}
