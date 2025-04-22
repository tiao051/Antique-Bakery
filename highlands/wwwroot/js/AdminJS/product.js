document.addEventListener('DOMContentLoaded', () => {
    setupChartFilterHandlers();
    setupRefreshHandler();
    setupImportExcelHandler();
    loadShoppingPatterns(3);
});

function setupChartFilterHandlers() {
    const filters = document.querySelectorAll('.chart-filter');
    filters.forEach(filter => {
        filter.addEventListener('click', () => {
            filters.forEach(f => f.classList.remove('active'));
            filter.classList.add('active');
            const maxItems = parseInt(filter.dataset.maxItems);
            loadShoppingPatterns(maxItems);
        });
    });
}

function setupRefreshHandler() {
    const refreshBtn = document.querySelector(".refresh-action");
    if (refreshBtn) {
        refreshBtn.addEventListener("click", () => location.reload());
    }
}

function setupImportExcelHandler() {
    const importBtn = document.querySelector(".import-action");
    const fileInput = document.getElementById("excelInput");

    if (importBtn && fileInput) {
        importBtn.addEventListener("click", () => fileInput.click());
        fileInput.addEventListener("change", () => handleExcelFile(fileInput.files[0]));
    }
}

function handleExcelFile(file) {
    if (!file) return;
    console.log("You selected:", file.name);
    // TODO: Upload file to server or parse client-side
}

function loadShoppingPatterns(maxResults = 3) {
    const loading = document.querySelector('.patterns-loading');
    const list = document.querySelector('.patterns-list');
    const empty = document.querySelector('.patterns-empty');

    showLoadingState(loading, list, empty);

    fetch(`/api/AdminApi/productSequences?topN=${maxResults}`)
        .then(res => {
            if (!res.ok) throw new Error('Failed to fetch shopping patterns');
            return res.json();
        })
        .then(data => {
            loading.style.display = 'none';

            if (!data || data.length === 0) {
                empty.style.display = 'block';
                return;
            }

            const mapped = data.map(p => {
                const sequence = p.combo.split(' | ').map(item => item.trim()); 
                return {
                    itemSequence: sequence,
                    frequency: p.count
                };
            });

            renderPatternsList(mapped);
            list.style.display = 'block';
        })
        .catch(error => {
            console.error('Error:', error);
            loading.style.display = 'none';
            empty.style.display = 'block';
            empty.querySelector('p').textContent = 'Error loading shopping patterns.';
        });
}

function showLoadingState(loading, list, empty) {
    loading.style.display = 'flex';
    list.style.display = 'none';
    empty.style.display = 'none';
}

function renderPatternsList(patterns) {
    const container = document.querySelector('.patterns-list');
    container.innerHTML = '';

    const max = Math.max(...patterns.map(p => p.frequency));
    const min = Math.min(...patterns.map(p => p.frequency));

    patterns.forEach((pattern, i) => {
        const intensity = (max === min) ? 100 :
            Math.round(((pattern.frequency - min) / (max - min)) * 100);

        const sequenceHTML = pattern.itemSequence.map((item, idx) => `
            <span class="pattern-item-name">${item}</span>
            ${idx < pattern.itemSequence.length - 1 ? '<i class="fas fa-arrow-right"></i>' : ''}
        `).join('');

        const html = `
        <div class="pattern-item">
            <div class="pattern-rank">#${i + 1}</div>
            <div class="pattern-sequence">${sequenceHTML}</div>
            <div class="pattern-frequency">
                <i class="fas fa-chart-line" style="margin-right: 8px;"></i> 
                ${pattern.frequency} occurrences
            </div>
            <div class="pattern-intensity" style="width: ${intensity}%"></div>
        </div>`;

        container.insertAdjacentHTML('beforeend', html);
    });
}
