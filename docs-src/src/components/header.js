import React, { Component } from "react";
import { Link } from "react-static";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faGithub, faTwitter } from "@fortawesome/free-brands-svg-icons";

export default class Header extends Component {

	render() {
		return (
			<header className="header">
				<nav className="navbar is-light">
					<div className="container is-fluid">
						<div className="navbar-brand">
							<Link className="navbar-item" to="/">
								<img src="/images/logo.svg" width="66" height="28" />
							</Link>

							<div className="navbar-burger burger">
								<span></span>
								<span></span>
								<span></span>
							</div>
						</div>

						<div className="navbar-menu">
							<div className="navbar-start">
								<Link className="navbar-item" to="/getting-started">Getting Started</Link>
								<Link className="navbar-item" to="/examples">Examples</Link>
								<Link className="navbar-item" to="/api">API</Link>
								<Link className="navbar-item" to="/change-log">Change Log</Link>
							</div>
							<div className="navbar-end">
								<a className="navbar-item" href="https://twitter.com/JoshClose">
									<span className="icon">
										<FontAwesomeIcon icon={faTwitter} />
									</span>
								</a>
								<a className="navbar-item" href="https://github.com/JoshClose/CsvHelper">
									<span className="icon">
										<FontAwesomeIcon icon={faGithub} />
									</span>
								</a>
							</div>
						</div>
					</div>
				</nav>

				{/*
				The actual navbar above is fixed so we need to add the same amount of space
				to the top of the page so it's not hidden behind the header.
				*/}
				<div className="navbar">
				</div>
			</header>
		)
	}

}