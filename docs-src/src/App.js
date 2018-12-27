import React from 'react'
import { Router } from 'react-static'
import { hot } from 'react-hot-loader'
import Routes from 'react-static-routes'

import Layout from "./components/layout"
import "./css/site.scss"

const App = () => (
	<Router scrollToHashDuration={0}>
		<Layout>
			<Routes />
		</Layout>
	</Router>
)

export default hot(module)(App)