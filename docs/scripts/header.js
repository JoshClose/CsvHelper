(() => {
	document.getElementById("navbar-burger").addEventListener("click", () => {
		document.getElementById("navbar-burger").classList.toggle("is-active");
		document.getElementById("navbar-menu").classList.toggle("is-active");
	});
});
