import React, { Component } from "react";
import { Link } from "react-static";

import data from "../data/menu";

export default class Menu extends Component {
	render() {
		return (
			<aside className="menu">
				{data.map((item, i) => (
					<p key={i} className="menu-label"><Link to={item.path}>{item.title}</Link></p>
				))}
			</aside>
		);
	}
};