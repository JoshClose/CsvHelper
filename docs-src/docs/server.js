const express = require("express");
const path = require("path");

const app = express();
const port = 3000;

app.use("/CsvHelper", express.static(path.join(__dirname, "dist")));

app.listen(port, () => console.log(`Express app listening on port ${port}`));
