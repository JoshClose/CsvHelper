$(function(){
	$("textarea").each(function(){
		var mode = $(this).data("code") || "text/x-csharp";
		CodeMirror.fromTextArea(this, {
			mode: mode,
			readOnly: true,
			lineNumbers: true,
			lineWrapping: true,
			viewportMargin: Infinity
		});
	});

	$("header .nav a[href!=#]").each(function(){
		$($(this).attr("href")).css("padding-top", "40px").prev().css("margin-bottom", "-40px");
	});
});