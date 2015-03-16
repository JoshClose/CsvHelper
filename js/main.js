$(function(){
	var renderer = new marked.Renderer();
	renderer.heading = function(text, level, raw){
		return "<h"
			+ level
			+ " id='"
			+ this.options.headerPrefix
			+ raw.toLowerCase().replace(/\[(.*?)\]/g, "$1").replace(/[^\w]+/g, "-")
			+ "'>"
			+ text.replace(/\[(.*?)\]/g, "")
			+ "</h"
			+ level
			+ ">\n";
	};

	marked.setOptions({
		highlight: function(code, language, callback){
			return prettyPrintOne(code, language);
		},
		renderer: renderer
	});

	$.ajax({
		contentType: "text/plain",
		dataType: "text",
		url: "markdown.txt"
	}).done(function(data){
		$("#markdown-output").html(marked(data));
		$("body").scrollspy();
		if(location.hash){
			$("body").scrollTop($(location.hash).offset().top);
		}
	});
});