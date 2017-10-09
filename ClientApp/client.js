import "./css/site.scss"
import * as React from "react"
import { render } from "react-dom"
import { Provider } from "react-redux"
import { createStore, applyMiddleware } from "redux"
import { ConnectedRouter, routerMiddleware } from "react-router-redux"
import { createBrowserHistory } from "history"
import { createLogger } from "redux-logger"

import reducers from "./reducers"
import Layout from "./components/layout"

const history = createBrowserHistory();
const logger = createLogger({
	collapsed: true
});
const store = createStore(reducers, applyMiddleware(routerMiddleware(history), logger));

render(
	<Provider store={store}>
		<ConnectedRouter history={history}>
			<Layout />
		</ConnectedRouter>
	</Provider>,
	document.getElementById("root")
)
