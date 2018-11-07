import React, { Component } from "react"
import { Link } from "react-static"

export default class Header extends Component {

	render() {
		return (
			<header className="header">
				<nav className="navbar is-light">
					<div className="container">
						<div className="navbar-brand">
							<Link className="navbar-item" to="/">
								<img src="/images/logo-header.png" width="66" height="28" />
							</Link>

							<div className="navbar-burger burger">
								<span></span>
								<span></span>
								<span></span>
							</div>
						</div>

						<div className="navbar-menu">
							<div className="navbar-start">
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/reading">Reading</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/reading#getting-all-records">Getting All Records</Link>
										<Link className="navbar-item" to="/reading#reading-records">Reading Records</Link>
										<Link className="navbar-item" to="/reading#getting-a-single-record">Getting a Single Record</Link>
										<Link className="navbar-item" to="/reading#getting-fields">Getting Fields</Link>
										<Link className="navbar-item" to="/reading#malformed-field-fallback">Malformed Field Fallback</Link>
										<Link className="navbar-item" to="/reading#reading-context">Reading Context</Link>
										<Link className="navbar-item" to="/reading#configuration">Configuration</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/writing">Writing</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/writing#writing-all-records">Writing All Records</Link>
										<Link className="navbar-item" to="/writing#writing-a-single-record">Writing a Single Record</Link>
										<Link className="navbar-item" to="/writing#writing-fields">Writing Fields</Link>
										<Link className="navbar-item" to="/writing#ending-the-row">Ending the Row</Link>
										<Link className="navbar-item" to="/writing#writing-context">Writing Context</Link>
										<Link className="navbar-item" to="/writing#configuration">Configuration</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/mapping">Mapping</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/mapping#reference-mapping">Reference Mapping</Link>
										<Link className="navbar-item" to="/mapping#auto-mapping">Auto Mapping</Link>
										<Link className="navbar-item" to="/mapping#options">Options</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/configuration">Configuration</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/configuration#malicious-injection-protection">Malicious Injection Protection</Link>
										<Link className="navbar-item" to="/configuration#headers">Headers</Link>
										<Link className="navbar-item" to="/configuration#mapping">Mapping</Link>
										<Link className="navbar-item" to="/configuration#constructor-mapping">Constructor Mapping</Link>
										<Link className="navbar-item" to="/configuration#error-handling">Error Handling</Link>
										<Link className="navbar-item" to="/configuration#type-conversion">Type Conversion</Link>
										<Link className="navbar-item" to="/configuration#reading">Reading</Link>
										<Link className="navbar-item" to="/configuration#parsing">Parsing</Link>
										<Link className="navbar-item" to="/configuration#writing">Writing</Link>
										<Link className="navbar-item" to="/configuration#formatting">Formatting</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/type-conversion">Type Conversion</Link>
									<div className="navbar-dropdown">
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<div className="navbar-link">Misc</div>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/examples">Examples</Link>
										<Link className="navbar-item" to="/change-log">Change Log</Link>
									</div>
								</div>
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
