/******/ (function(modules) { // webpackBootstrap
/******/ 	// The module cache
/******/ 	var installedModules = {};
/******/
/******/ 	// The require function
/******/ 	function __webpack_require__(moduleId) {
/******/
/******/ 		// Check if module is in cache
/******/ 		if(installedModules[moduleId]) {
/******/ 			return installedModules[moduleId].exports;
/******/ 		}
/******/ 		// Create a new module (and put it into the cache)
/******/ 		var module = installedModules[moduleId] = {
/******/ 			i: moduleId,
/******/ 			l: false,
/******/ 			exports: {}
/******/ 		};
/******/
/******/ 		// Execute the module function
/******/ 		modules[moduleId].call(module.exports, module, module.exports, __webpack_require__);
/******/
/******/ 		// Flag the module as loaded
/******/ 		module.l = true;
/******/
/******/ 		// Return the exports of the module
/******/ 		return module.exports;
/******/ 	}
/******/
/******/
/******/ 	// expose the modules object (__webpack_modules__)
/******/ 	__webpack_require__.m = modules;
/******/
/******/ 	// expose the module cache
/******/ 	__webpack_require__.c = installedModules;
/******/
/******/ 	// define getter function for harmony exports
/******/ 	__webpack_require__.d = function(exports, name, getter) {
/******/ 		if(!__webpack_require__.o(exports, name)) {
/******/ 			Object.defineProperty(exports, name, {
/******/ 				configurable: false,
/******/ 				enumerable: true,
/******/ 				get: getter
/******/ 			});
/******/ 		}
/******/ 	};
/******/
/******/ 	// getDefaultExport function for compatibility with non-harmony modules
/******/ 	__webpack_require__.n = function(module) {
/******/ 		var getter = module && module.__esModule ?
/******/ 			function getDefault() { return module['default']; } :
/******/ 			function getModuleExports() { return module; };
/******/ 		__webpack_require__.d(getter, 'a', getter);
/******/ 		return getter;
/******/ 	};
/******/
/******/ 	// Object.prototype.hasOwnProperty.call
/******/ 	__webpack_require__.o = function(object, property) { return Object.prototype.hasOwnProperty.call(object, property); };
/******/
/******/ 	// __webpack_public_path__
/******/ 	__webpack_require__.p = "/dist/";
/******/
/******/ 	// Load entry module and return exports
/******/ 	return __webpack_require__(__webpack_require__.s = 5);
/******/ })
/************************************************************************/
/******/ ([
/* 0 */
/***/ (function(module, exports) {

module.exports = vendor_aca7704a4987ba32fb74;

/***/ }),
/* 1 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(4);

/***/ }),
/* 2 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(241);

/***/ }),
/* 3 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(285);

/***/ }),
/* 4 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(106);

/***/ }),
/* 5 */
/***/ (function(module, exports, __webpack_require__) {

"use strict";


__webpack_require__(6);

var _react = __webpack_require__(1);

var React = _interopRequireWildcard(_react);

var _reactDom = __webpack_require__(7);

var _reactRedux = __webpack_require__(2);

var _redux = __webpack_require__(4);

var _reactRouterRedux = __webpack_require__(3);

var _history = __webpack_require__(8);

var _reduxLogger = __webpack_require__(9);

var _reducers = __webpack_require__(10);

var _reducers2 = _interopRequireDefault(_reducers);

var _layout = __webpack_require__(11);

var _layout2 = _interopRequireDefault(_layout);

function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }

function _interopRequireWildcard(obj) { if (obj && obj.__esModule) { return obj; } else { var newObj = {}; if (obj != null) { for (var key in obj) { if (Object.prototype.hasOwnProperty.call(obj, key)) newObj[key] = obj[key]; } } newObj.default = obj; return newObj; } }

var history = (0, _history.createBrowserHistory)();
var logger = (0, _reduxLogger.createLogger)({
	collapsed: true
});
var store = (0, _redux.createStore)(_reducers2.default, (0, _redux.applyMiddleware)((0, _reactRouterRedux.routerMiddleware)(history), logger));

(0, _reactDom.render)(React.createElement(
	_reactRedux.Provider,
	{ store: store },
	React.createElement(
		_reactRouterRedux.ConnectedRouter,
		{ history: history },
		React.createElement(_layout2.default, null)
	)
), document.getElementById("root"));

/***/ }),
/* 6 */
/***/ (function(module, exports) {

// removed by extract-text-webpack-plugin

/***/ }),
/* 7 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(76);

/***/ }),
/* 8 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(121);

/***/ }),
/* 9 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(289);

/***/ }),
/* 10 */
/***/ (function(module, exports, __webpack_require__) {

"use strict";


Object.defineProperty(exports, "__esModule", {
	value: true
});

var _redux = __webpack_require__(4);

var _reactRouterRedux = __webpack_require__(3);

exports.default = (0, _redux.combineReducers)({
	router: _reactRouterRedux.routerReducer
});

/***/ }),
/* 11 */
/***/ (function(module, exports, __webpack_require__) {

"use strict";


Object.defineProperty(exports, "__esModule", {
	value: true
});

var _createClass = function () { function defineProperties(target, props) { for (var i = 0; i < props.length; i++) { var descriptor = props[i]; descriptor.enumerable = descriptor.enumerable || false; descriptor.configurable = true; if ("value" in descriptor) descriptor.writable = true; Object.defineProperty(target, descriptor.key, descriptor); } } return function (Constructor, protoProps, staticProps) { if (protoProps) defineProperties(Constructor.prototype, protoProps); if (staticProps) defineProperties(Constructor, staticProps); return Constructor; }; }();

var _react = __webpack_require__(1);

var _react2 = _interopRequireDefault(_react);

var _reactRedux = __webpack_require__(2);

var _reactRouter = __webpack_require__(12);

var _reactRouterDom = __webpack_require__(17);

var _reactRouterRedux = __webpack_require__(3);

var _isomorphicFetch = __webpack_require__(13);

var _isomorphicFetch2 = _interopRequireDefault(_isomorphicFetch);

var _marked = __webpack_require__(14);

var _marked2 = _interopRequireDefault(_marked);

var _highlight2 = __webpack_require__(15);

var _highlight3 = _interopRequireDefault(_highlight2);

var _header = __webpack_require__(16);

var _header2 = _interopRequireDefault(_header);

var _footer = __webpack_require__(18);

var _footer2 = _interopRequireDefault(_footer);

function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }

function _classCallCheck(instance, Constructor) { if (!(instance instanceof Constructor)) { throw new TypeError("Cannot call a class as a function"); } }

function _possibleConstructorReturn(self, call) { if (!self) { throw new ReferenceError("this hasn't been initialised - super() hasn't been called"); } return call && (typeof call === "object" || typeof call === "function") ? call : self; }

function _inherits(subClass, superClass) { if (typeof superClass !== "function" && superClass !== null) { throw new TypeError("Super expression must either be null or a function, not " + typeof superClass); } subClass.prototype = Object.create(superClass && superClass.prototype, { constructor: { value: subClass, enumerable: false, writable: true, configurable: true } }); if (superClass) Object.setPrototypeOf ? Object.setPrototypeOf(subClass, superClass) : subClass.__proto__ = superClass; }

function wrapInColumns(text) {
	return "<div class=\"columns\"><div class=\"column\">" + text + "</div></div>";
}

function htmlEncode(text) {
	return text.replace(/</g, "&lt;").replace(/>/g, "&gt");
}

function toSeoFriendly(text) {
	var result = text.toLowerCase().match(/[\w]+/g).map(function (word) {
		return encodeURIComponent(word);
	}).join("-");
	return result;
}

_highlight3.default.configure({
	languages: ["cs"]
});
var renderer = new _marked2.default.Renderer();
renderer.blockquote = function (quote) {
	return "<div class=\"content\"><blockquote>" + quote + "</blockquote></div>";
};
// For some reason using `this` here when this
// is a lambda function, `this` is undefined,
// so using a normal function.
renderer.code = function (code, lang) {
	if (this.options.highlight) {
		code = this.options.highlight(code, lang) || code;
	}

	return wrapInColumns("<pre><code class=\"box " + lang + "\">" + code + "</code></pre>");
};
renderer.heading = function (text, level) {
	return "<h" + level + " id=\"" + toSeoFriendly(text) + "\" class=\"title is-" + level + "\"><span>" + htmlEncode(text) + "</span></h" + level + ">";
};
renderer.link = function (href, title, text) {
	return "<a href=\"" + href + "\" target=\"" + (/^[\/#].*/.test(href) ? "_self" : "_blank") + "\">" + text + "</a>";
};
renderer.list = function (body, ordered) {
	return ordered ? "<div class=\"content\"><ol>" + body + "</ol></div>" : "<div class=\"content\"><ul>" + body + "</ul></div>";
};
renderer.paragraph = function (text) {
	return wrapInColumns("<p>" + text + "</p>");
};
_marked2.default.setOptions({
	renderer: renderer,
	highlight: function highlight(code, language, callback) {
		//code = code.replace(/</g, "&lt;").replace(/>/g, "&gt;");
		if (language) {
			return _highlight3.default.highlight(language, code, true).value;
		}

		return _highlight3.default.highlightAuto(code).value;
	}
});

var Layout = function (_Component) {
	_inherits(Layout, _Component);

	function Layout(props) {
		_classCallCheck(this, Layout);

		var _this = _possibleConstructorReturn(this, (Layout.__proto__ || Object.getPrototypeOf(Layout)).call(this, props));

		_this.state = {
			content: "",
			page: ""
		};

		_this.loadPage = function (location) {
			var match = location.pathname.match(/\/CsvHelper\/([^\?#/]+).*/);
			var page = !match ? "home" : match[1];

			(0, _isomorphicFetch2.default)("/CsvHelper/pages/" + page + ".md", {
				method: "get",
				credentials: "same-origin",
				headers: {
					"Content-Type": "text/plain"
				}
			}).then(function (response) {
				return response.text();
			}).then(function (content) {
				_this.setState({ content: content, page: page }, _this.scrollToHash);
			});
		};

		_this.scrollToHash = function () {
			if (!_this.props.location.hash) {
				window.scroll(0, 0);
				return;
			}

			var hash = _this.props.location.hash.substr(1);
			var element = document.getElementById(hash);
			if (element) {
				element.scrollIntoView(true);
			}
		};

		_this.loadPage(_this.props.history.location);
		_this.props.history.listen(_this.loadPage);
		return _this;
	}

	_createClass(Layout, [{
		key: "render",
		value: function render() {
			var _state = this.state,
			    content = _state.content,
			    page = _state.page;

			var markdown = (0, _marked2.default)(content);

			return _react2.default.createElement(
				"div",
				{ className: "layout" },
				_react2.default.createElement(
					"a",
					{ className: "fork-me-on-github", href: "https://github.com/joshclose/csvhelper", target: "_blank" },
					_react2.default.createElement("img", { src: "https://s3.amazonaws.com/github/ribbons/forkme_right_gray_6d6d6d.png", alt: "Fork me on GitHub" })
				),
				_react2.default.createElement(_header2.default, null),
				_react2.default.createElement("div", { id: "top-of-page" }),
				_react2.default.createElement(
					"div",
					{ className: "container" },
					_react2.default.createElement(
						_reactRouterDom.Switch,
						null,
						_react2.default.createElement(_reactRouterDom.Route, { path: "/", render: function render() {
								return _react2.default.createElement("div", { className: page, dangerouslySetInnerHTML: { __html: markdown } });
							} })
					)
				),
				_react2.default.createElement(_footer2.default, null)
			);
		}
	}]);

	return Layout;
}(_react.Component);

exports.default = (0, _reactRouter.withRouter)((0, _reactRedux.connect)()(Layout));

/***/ }),
/* 12 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(287);

/***/ }),
/* 13 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(469);

/***/ }),
/* 14 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(471);

/***/ }),
/* 15 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(290);

/***/ }),
/* 16 */
/***/ (function(module, exports, __webpack_require__) {

"use strict";


Object.defineProperty(exports, "__esModule", {
	value: true
});

var _createClass = function () { function defineProperties(target, props) { for (var i = 0; i < props.length; i++) { var descriptor = props[i]; descriptor.enumerable = descriptor.enumerable || false; descriptor.configurable = true; if ("value" in descriptor) descriptor.writable = true; Object.defineProperty(target, descriptor.key, descriptor); } } return function (Constructor, protoProps, staticProps) { if (protoProps) defineProperties(Constructor.prototype, protoProps); if (staticProps) defineProperties(Constructor, staticProps); return Constructor; }; }();

var _react = __webpack_require__(1);

var _react2 = _interopRequireDefault(_react);

var _reactRedux = __webpack_require__(2);

var _reactRouterDom = __webpack_require__(17);

function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }

function _classCallCheck(instance, Constructor) { if (!(instance instanceof Constructor)) { throw new TypeError("Cannot call a class as a function"); } }

function _possibleConstructorReturn(self, call) { if (!self) { throw new ReferenceError("this hasn't been initialised - super() hasn't been called"); } return call && (typeof call === "object" || typeof call === "function") ? call : self; }

function _inherits(subClass, superClass) { if (typeof superClass !== "function" && superClass !== null) { throw new TypeError("Super expression must either be null or a function, not " + typeof superClass); } subClass.prototype = Object.create(superClass && superClass.prototype, { constructor: { value: subClass, enumerable: false, writable: true, configurable: true } }); if (superClass) Object.setPrototypeOf ? Object.setPrototypeOf(subClass, superClass) : subClass.__proto__ = superClass; }

var Header = function (_Component) {
	_inherits(Header, _Component);

	function Header() {
		_classCallCheck(this, Header);

		return _possibleConstructorReturn(this, (Header.__proto__ || Object.getPrototypeOf(Header)).apply(this, arguments));
	}

	_createClass(Header, [{
		key: "render",
		value: function render() {
			return _react2.default.createElement(
				"header",
				{ className: "header" },
				_react2.default.createElement(
					"div",
					{ className: "container" },
					_react2.default.createElement(
						"nav",
						{ className: "navbar" },
						_react2.default.createElement(
							"div",
							{ className: "navbar-brand" },
							_react2.default.createElement(
								"a",
								{ className: "navbar-item", href: "/CsvHelper/" },
								_react2.default.createElement("img", { src: "/CsvHelper/images/logo-header.png", width: "66", height: "28" })
							),
							_react2.default.createElement(
								"div",
								{ className: "navbar-burger burger" },
								_react2.default.createElement("span", null),
								_react2.default.createElement("span", null),
								_react2.default.createElement("span", null)
							)
						),
						_react2.default.createElement(
							"div",
							{ className: "navbar-menu" },
							_react2.default.createElement(
								"div",
								{ className: "navbar-start" },
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										_reactRouterDom.Link,
										{ className: "navbar-link", to: "/CsvHelper/reading" },
										"Reading"
									),
									_react2.default.createElement(
										"div",
										{ className: "navbar-dropdown" },
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#getting-all-records" },
											"Getting All Records"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#reading-records" },
											"Reading Records"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#getting-a-single-record" },
											"Getting a Single Record"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#getting-fields" },
											"Getting Fields"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#malformed-field-fallback" },
											"Malformed Field Fallback"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#reading-context" },
											"Reading Context"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/reading#configuration" },
											"Configuration"
										)
									)
								),
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										_reactRouterDom.Link,
										{ className: "navbar-link", to: "/CsvHelper/writing" },
										"Writing"
									),
									_react2.default.createElement(
										"div",
										{ className: "navbar-dropdown" },
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#writing-all-records" },
											"Writing All Records"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#writing-a-single-record" },
											"Writing a Single Record"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#writing-fields" },
											"Writing Fields"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#ending-the-row" },
											"Ending the Row"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#writing-context" },
											"Writing Context"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/writing#configuration" },
											"Configuration"
										)
									)
								),
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										_reactRouterDom.Link,
										{ className: "navbar-link", to: "/CsvHelper/mapping" },
										"Mapping"
									),
									_react2.default.createElement(
										"div",
										{ className: "navbar-dropdown" },
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/mapping#reference-mapping" },
											"Reference Mapping"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/mapping#auto-mapping" },
											"Auto Mapping"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/mapping#options" },
											"Options"
										)
									)
								),
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										_reactRouterDom.Link,
										{ className: "navbar-link", to: "/CsvHelper/configuration" },
										"Configuration"
									),
									_react2.default.createElement(
										"div",
										{ className: "navbar-dropdown" },
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#malicious-injection-protection" },
											"Malicious Injection Protection"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#headers" },
											"Headers"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#mapping" },
											"Mapping"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#constructor-mapping" },
											"Constructor Mapping"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#error-handling" },
											"Error Handling"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#type-conversion" },
											"Type Conversion"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#reading" },
											"Reading"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#parsing" },
											"Parsing"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#writing" },
											"Writing"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/configuration#formatting" },
											"Formatting"
										)
									)
								),
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										_reactRouterDom.Link,
										{ className: "navbar-link", to: "/CsvHelper/type-conversion" },
										"Type Conversion"
									),
									_react2.default.createElement("div", { className: "navbar-dropdown" })
								),
								_react2.default.createElement(
									"div",
									{ className: "navbar-item has-dropdown is-hoverable" },
									_react2.default.createElement(
										"div",
										{ className: "navbar-link" },
										"Misc"
									),
									_react2.default.createElement(
										"div",
										{ className: "navbar-dropdown" },
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/examples" },
											"Examples"
										),
										_react2.default.createElement(
											_reactRouterDom.Link,
											{ className: "navbar-item", to: "/CsvHelper/change-log" },
											"Change Log"
										)
									)
								)
							)
						)
					)
				),
				_react2.default.createElement("div", { className: "navbar" })
			);
		}
	}]);

	return Header;
}(_react.Component);

exports.default = (0, _reactRedux.connect)()(Header);

/***/ }),
/* 17 */
/***/ (function(module, exports, __webpack_require__) {

module.exports = (__webpack_require__(0))(266);

/***/ }),
/* 18 */
/***/ (function(module, exports, __webpack_require__) {

"use strict";


Object.defineProperty(exports, "__esModule", {
	value: true
});

var _createClass = function () { function defineProperties(target, props) { for (var i = 0; i < props.length; i++) { var descriptor = props[i]; descriptor.enumerable = descriptor.enumerable || false; descriptor.configurable = true; if ("value" in descriptor) descriptor.writable = true; Object.defineProperty(target, descriptor.key, descriptor); } } return function (Constructor, protoProps, staticProps) { if (protoProps) defineProperties(Constructor.prototype, protoProps); if (staticProps) defineProperties(Constructor, staticProps); return Constructor; }; }();

var _react = __webpack_require__(1);

var _react2 = _interopRequireDefault(_react);

var _reactRedux = __webpack_require__(2);

function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }

function _classCallCheck(instance, Constructor) { if (!(instance instanceof Constructor)) { throw new TypeError("Cannot call a class as a function"); } }

function _possibleConstructorReturn(self, call) { if (!self) { throw new ReferenceError("this hasn't been initialised - super() hasn't been called"); } return call && (typeof call === "object" || typeof call === "function") ? call : self; }

function _inherits(subClass, superClass) { if (typeof superClass !== "function" && superClass !== null) { throw new TypeError("Super expression must either be null or a function, not " + typeof superClass); } subClass.prototype = Object.create(superClass && superClass.prototype, { constructor: { value: subClass, enumerable: false, writable: true, configurable: true } }); if (superClass) Object.setPrototypeOf ? Object.setPrototypeOf(subClass, superClass) : subClass.__proto__ = superClass; }

var Footer = function (_Component) {
	_inherits(Footer, _Component);

	function Footer() {
		_classCallCheck(this, Footer);

		return _possibleConstructorReturn(this, (Footer.__proto__ || Object.getPrototypeOf(Footer)).apply(this, arguments));
	}

	_createClass(Footer, [{
		key: "render",
		value: function render() {
			return _react2.default.createElement("footer", { className: "footer" });
		}
	}]);

	return Footer;
}(_react.Component);

exports.default = (0, _reactRedux.connect)()(Footer);

/***/ })
/******/ ]);
//# sourceMappingURL=client.js.map