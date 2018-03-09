import React from "react"
import fs from "fs"
import axios from "axios"
import ExtractTextPlugin from "extract-text-webpack-plugin"

function createData(name) {
	return () => ({
		className: name,
		data: fs.readFileSync(`./src/pages/${name}.md`, "utf-8")
	});
}

export default {
	siteRoot: "http://joshclose.github.io",
	basePath: "/CsvHelper/test/",
	getRoutes: async () => {
		return [
			{
				path: "",
				component: "src/components/content",
				getData: createData("home")
			},
			{
				path: "reading",
				component: "src/components/content",
				getData: createData("reading")
			},
			{
				path: "writing",
				component: "src/components/content",
				getData: createData("writing")
			},
			{
				path: "mapping",
				component: "src/components/content",
				getData: createData("mapping")
			},
			{
				path: "configuration",
				component: "src/components/content",
				getData: createData("configuration")
			},
			{
				path: "type-conversion",
				component: "src/components/content",
				getData: createData("type-conversion")
			},
			{
				path: "examples",
				component: "src/components/content",
				getData: createData("examples")
			},
			{
				path: "change-log",
				component: "src/components/content",
				getData: createData("change-log")
			},
			{
				is404: true,
				component: 'src/components/404',
			}
		]
	},
	webpack: (config, { defaultLoaders, stage }) => {
		config.module.rules = [{
			oneOf: [
				{
					test: /\.s[ac]ss$/,
					use:
						stage === 'dev'
							? [{ loader: 'style-loader' }, { loader: 'css-loader' }, { loader: 'sass-loader' }]
							: ExtractTextPlugin.extract({
								use: [
									{
										loader: 'css-loader',
										options: {
											importLoaders: 1,
											minimize: true,
											sourceMap: false,
										},
									},
									{
										loader: 'sass-loader',
										options: { includePaths: ['src/'] },
									},
								],
							}),
				},
				defaultLoaders.cssLoader,
				defaultLoaders.jsLoader,
				defaultLoaders.fileLoader,
			],
		}]
		return config
	}
}
