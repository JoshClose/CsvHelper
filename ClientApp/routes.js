import React, { Component } from "react"
import { connect } from "react-redux"
import { withRouter } from "react-router"
import { Route } from "react-router-dom"

import Layout from "./components/layout"
import Home from "./components/home"

class Routes extends Component {

	render() {
		return (
			<Layout>
			</Layout>
		)
	}

}

export default withRouter(connect()(Routes))