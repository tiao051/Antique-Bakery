document.addEventListener('DOMContentLoaded', () => {
    const dropdown = document.querySelector('.report-dropdown');
    const options = document.getElementById('reportOptions');

    setupDropdownToggle(dropdown, options);
    setupHoverAutoClose(dropdown, options);
});

function setupDropdownToggle(dropdown, options) {
    const trigger = dropdown.querySelector('.header-action');
    trigger.addEventListener('click', () => {
        toggleVisibility(options);
    });
}

function setupHoverAutoClose(dropdown, options) {
    let hideTimeout;

    dropdown.addEventListener('mouseleave', () => {
        hideTimeout = setTimeout(() => {
            hideElement(options);
        }, 300); 
    });

    dropdown.addEventListener('mouseenter', () => {
        clearTimeout(hideTimeout); 
    });
}

function toggleVisibility(element) {
    const isVisible = element.style.display === 'block';
    element.style.display = isVisible ? 'none' : 'block';
}

function hideElement(element) {
    element.style.display = 'none';
}

function exportReport(type) {
    window.location.href = `/Report/DownloadReport?type=${type}`;
}


