import React, { Component } from "react"
import { connect } from "react-redux"
import { Link, NavLink } from "react-router-dom"

class Header extends Component {

	render() {
		return (
			<header className="header">
				<div className="container">
					<nav className="navbar">
						<div className="navbar-brand">
							<a className="navbar-item" href="/CsvHelper/">
								<img src="/CsvHelper/images/logo-header.png" width="66" height="28" />
							</a>

							<div className="navbar-burger burger">
								<span></span>
								<span></span>
								<span></span>
							</div>
						</div>

						<div className="navbar-menu">
							<div className="navbar-start">
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/CsvHelper/reading">Reading</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/CsvHelper/reading#getting-all-records">Getting All Records</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#reading-records">Reading Records</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#getting-a-single-record">Getting a Single Record</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#getting-fields">Getting Fields</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#malformed-field-fallback">Malformed Field Fallback</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#reading-context">Reading Context</Link>
										<Link className="navbar-item" to="/CsvHelper/reading#configuration">Configuration</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/CsvHelper/writing">Writing</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/CsvHelper/writing#writing-all-records">Writing All Records</Link>
										<Link className="navbar-item" to="/CsvHelper/writing#writing-a-single-record">Writing a Single Record</Link>
										<Link className="navbar-item" to="/CsvHelper/writing#writing-fields">Writing Fields</Link>
										<Link className="navbar-item" to="/CsvHelper/writing#ending-the-row">Ending the Row</Link>
										<Link className="navbar-item" to="/CsvHelper/writing#writing-context">Writing Context</Link>
										<Link className="navbar-item" to="/CsvHelper/writing#configuration">Configuration</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/CsvHelper/mapping">Mapping</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/CsvHelper/mapping#reference-mapping">Reference Mapping</Link>
										<Link className="navbar-item" to="/CsvHelper/mapping#auto-mapping">Auto Mapping</Link>
										<Link className="navbar-item" to="/CsvHelper/mapping#options">Options</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/CsvHelper/configuration">Configuration</Link>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/CsvHelper/configuration#malicious-injection-protection">Malicious Injection Protection</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#headers">Headers</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#mapping">Mapping</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#constructor-mapping">Constructor Mapping</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#error-handling">Error Handling</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#type-conversion">Type Conversion</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#reading">Reading</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#parsing">Parsing</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#writing">Writing</Link>
										<Link className="navbar-item" to="/CsvHelper/configuration#formatting">Formatting</Link>
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<Link className="navbar-link" to="/CsvHelper/type-conversion">Type Conversion</Link>
									<div className="navbar-dropdown">
									</div>
								</div>
								<div className="navbar-item has-dropdown is-hoverable">
									<div className="navbar-link">Misc</div>
									<div className="navbar-dropdown">
										<Link className="navbar-item" to="/CsvHelper/examples">Examples</Link>
										<Link className="navbar-item" to="/CsvHelper/change-log">Change Log</Link>
									</div>
								</div>
							</div>
						</div>
					</nav>
				</div>

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

export default connect()(Header)