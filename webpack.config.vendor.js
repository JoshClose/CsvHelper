const path = require("path");
const webpack = require("webpack");
const ExtractTextPlugin = require("extract-text-webpack-plugin");
const merge = require("webpack-merge");

module.exports = (env) => {
	const isDevBuild = !(env && env.prod);
	const extractCSS = new ExtractTextPlugin("vendor.css");

	const sharedConfig = {
		stats: { modules: false },
		resolve: { extensions: [".js"] },
		module: {
			rules: [
				{ test: /\.(png|woff|woff2|eot|ttf|svg)(\?|$)/, use: "url-loader?limit=100000" }
			]
		},
		entry: {
			vendor: [
				"classnames",
				"font-awesome/css/font-awesome.css",
				"highlight.js",
				"highlight.js/styles/vs.css",
				"history",
				"isomorphic-fetch",
				"marked",
				"prop-types",
				"query-string",
				"react",
				"react-addons-css-transition-group",
				"react-addons-transition-group",
				"react-dom",
				"react-redux",
				"react-router-dom",
				"react-router-redux",
				"redux",
				"redux-logger"
			]
		},
		output: {
			publicPath: "/dist/",
			filename: "[name].js",
			library: "[name]_[hash]"
		},
		plugins: [
			new webpack.ProvidePlugin({
				//$: "jquery",
				//jQuery: "jquery",
				//"window.jQuery": "jquery"
			}),
			new webpack.NormalModuleReplacementPlugin(/\/iconv-loader$/, require.resolve("node-noop")), // Workaround for https://github.com/andris9/encoding/issues/16
			new webpack.DefinePlugin({
				"process.env.NODE_ENV": isDevBuild ? '"development"' : '"production"'
			})
		]
	};

	const clientBundleConfig = merge(sharedConfig, {
		output: { path: path.join(__dirname, "dist") },
		module: {
			rules: [
				{ test: /\.css(\?|$)/, use: extractCSS.extract({ use: isDevBuild ? "css-loader" : "css-loader?minimize" }) }
			]
		},
		plugins: [
			extractCSS,
			new webpack.DllPlugin({
				path: path.join(__dirname, "dist", "[name]-manifest.json"),
				name: "[name]_[hash]"
			})
		].concat(isDevBuild ? [] : [
			new webpack.optimize.UglifyJsPlugin()
		])
	});

	return [clientBundleConfig];
};