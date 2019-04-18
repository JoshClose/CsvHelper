import React, { Component } from "react";
import { withRouteData } from "react-static";

import Content from "../components/content";
import TableOfContents from "../components/table-of-contents";

class Documentation extends Component {

	state = {
		sidebarBottom: 0
	}

	componentDidMount() {
		window.addEventListener("scroll", this.handleWindowScroll);

		this.footerEl = document.querySelector("#root > footer");

		this.updateSidebarBottom();
	}

	componentWillUnmount() {
		window.removeEventListener("scroll", this.handleWindowScroll);
	}

	updateSidebarBottom() {
		const visibleHeight = window.innerHeight - this.footerEl.getBoundingClientRect().top;
		const sidebarBottom = Math.max(0, visibleHeight);

		this.setState({ sidebarBottom });
	}

	handleWindowScroll = () => {
		this.updateSidebarBottom();
	}

	render() {
		const { sidebarBottom } = this.state;

		return (
			<div className="documentation container is-fluid">
				<div className="columns">
					<div className="column container-right">
						<div className="sidebar" style={{ bottom: sidebarBottom }}>
							<TableOfContents />
						</div>
					</div>
					<div className="column container-content">
						<Content />
						<br />
					</div>
				</div>
			</div>
		);
	}
};

export default withRouteData(Documentation);