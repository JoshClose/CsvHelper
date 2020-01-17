import React, { Component, Fragment } from "react";
import { withRouteData } from "react-static";
import { withRouter } from "react-router";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faAngleUp, faAngleDown } from "@fortawesome/free-solid-svg-icons";
import classNames from "classnames";

class Toc extends Component {

	state = {
		openTocItems: []
	}

	componentDidMount() {
		this.setDefaultOpenTocItems();
	}

	setDefaultOpenTocItems() {
		const { toc } = this.props;
		const openTocItems = [];
		this.updateOpenTocItemsForRoute(toc, openTocItems);

		this.setState({ openTocItems });
	}

	updateOpenTocItemsForRoute(item, openTocItems) {
		const routePath = this.props.location.pathname;

		if (routePath.startsWith(`/${item.path}/`)) {
			openTocItems.push(item.path);
		}

		if (item.children) {
			item.children.forEach(child => this.updateOpenTocItemsForRoute(child, openTocItems));
		}
	}

	handleItemToggleClick = (item) => {
		const itemIndex = this.state.openTocItems.indexOf(item.path);

		const openTocItems = itemIndex === -1
			? [...this.state.openTocItems, item.path]
			: [...this.state.openTocItems].filter(i => i !== item.path)

		this.setState({ openTocItems });
	}

	renderTocItems(items, nestingIndex) {
		const path = this.props.location.pathname;
		const { openTocItems } = this.state;

		return (
			<Fragment>
				{items.map((item, i) => (
					<Fragment key={i}>
						<div className={classNames("toc-item", { "is-current": path === `/${item.path}` })} style={{ paddingLeft: (20 * nestingIndex + 10) }}>
							{item.children && (
								<a className="toc-item-toggle" onClick={this.handleItemToggleClick.bind(this, item)}>
									<span className="icon">
										{openTocItems.includes(item.path) && (
											<FontAwesomeIcon icon={faAngleUp} />
										)}
										{!openTocItems.includes(item.path) && (
											<FontAwesomeIcon icon={faAngleDown} />
										)}
									</span>
								</a>
							)}

							<Link className="toc-item-name" to={`/${item.path}`}>{item.title}</Link>
						</div>

						{openTocItems.includes(item.path) && (
							<Fragment>
								{item.children && this.renderTocItems(item.children, nestingIndex + 1)}
							</Fragment>
						)}
					</Fragment>
				))}
			</Fragment>
		);
	}

	render() {
		const { toc } = this.props;

		return (
			<Fragment>
				{toc && (
					<div className="toc">
						{this.renderTocItems(toc.children, 0)}
					</div>
				)}
			</Fragment>
		);
	}
};

export default withRouter(withRouteData(Toc))
