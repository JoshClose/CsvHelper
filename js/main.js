//$(window).on("hashchange", function(){ window.scrollBy(0, -40) });
$(function(){
	$("textarea[data-code]").each(function(){
		var mode = $(this).data("code");
		CodeMirror.fromTextArea(this, {
			mode: mode,
			readOnly: "nocursor",
			lineNumbers: true,
			lineWrapping: true,
			viewportMargin: Infinity
		});
	});

	$("header .nav a[href!=#]").each(function(){
		$($(this).attr("href")).css("padding-top", "40px").prev().css("margin-bottom", "-40px");
	});
});