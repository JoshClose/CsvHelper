import React from "react"
import { Root, Routes, addPrefetchExcludes } from "react-static"
import { Switch, Route } from "react-router-dom";
import Layout from "./components/layout";
import "./css/site.scss"

// Any routes that start with "dynamic" will be treated as non-static routes
addPrefetchExcludes(["dynamic"])

function App(props) {
	return (
		<Root>
			<React.Suspense fallback={<em>Loading...</em>}>
				<Layout>
					<Switch>
						<Route render={() => <Routes />} />
					</Switch>
				</Layout>
			</React.Suspense>
		</Root>
	)
}

export default App
