const express = require("express");
const app = express();
const path = require("path");

const port = process.env.PORT || 3002;

app.use("/CsvHelper", express.static("./"));

app.get("/", function (req, res) {
	res.sendFile(path.join(__dirname, "index.html"));
});

app.listen(port, function () {
	console.log("Listen on port " + port);
});
