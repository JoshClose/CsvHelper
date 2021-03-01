(() => {
	const sidebar = document.getElementById("sidebar");
	if (!sidebar) {
		return;
	}

	const parent = sidebar.parentElement;
	const footer = document.getElementById("footer");

	const resizeSidebarWidth = () => {
		const rect = parent.getBoundingClientRect();
		const style = getComputedStyle(parent);
		const width = rect.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
		sidebar.style.width = `${width}px`;
	};

	const resizeSidebarHeight = () => {
		const parentRect = parent.getBoundingClientRect();
		const parentStyle = getComputedStyle(parent);
		const footerRect = footer.getBoundingClientRect();
		const bodyRect = document.body.getBoundingClientRect();

		const top = Math.max(parentRect.top + parseFloat(parentStyle.paddingTop), 0);
		const bottom = Math.max(bodyRect.height - footerRect.top, 0);

		sidebar.style.top = `${top}px`;
		sidebar.style.bottom = `${bottom}px`;
	}

	const handleExpanderClick = (e) => {
		e.currentTarget.classList.toggle("expanded");
		e.currentTarget.closest("li").querySelector("ul").classList.toggle("is-hidden");
	}

	const sidebarResizeObserver = new ResizeObserver(resizeSidebarWidth);
	sidebarResizeObserver.observe(parent);

	window.addEventListener("load", resizeSidebarHeight);
	document.addEventListener("scroll", resizeSidebarHeight);
	document.querySelectorAll(".expander").forEach(el => el.addEventListener("click", handleExpanderClick));
})();
