// CreateProduct.js - Manager Product Creation Scripts
document.addEventListener('DOMContentLoaded', function() {
    // Load categories from database
    loadCategories();
    
    // Load types from database
    loadTypes();
    
    // Handle upload method tabs
    setupUploadTabs();
    
    // Handle category change
    document.getElementById('category').addEventListener('change', function() {
        const category = this.value;
        loadSubcategories(category);
    });
    
    // Auto-hide alerts after 5 seconds
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.opacity = '0';
            setTimeout(() => {
                alert.remove();
            }, 300);
        }, 5000);
    });
});

// Load categories from API
async function loadCategories() {
    try {
        const response = await fetch('/Manager/GetCategories');
        const categories = await response.json();
        
        const categorySelect = document.getElementById('category');
        categorySelect.innerHTML = '<option value="">Select Category</option>';
        
        categories.forEach(category => {
            const option = document.createElement('option');
            option.value = category;
            option.textContent = category;
            categorySelect.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading categories:', error);
    }
}

// Load subcategories based on selected category
async function loadSubcategories(category) {
    const subcategorySelect = document.getElementById('subcategory');
    
    // Clear existing options
    subcategorySelect.innerHTML = '<option value="">Select Subcategory</option>';
    
    if (!category) return;
    
    try {
        const response = await fetch(`/Manager/GetSubcategories?categoryName=${encodeURIComponent(category)}`);
        const subcategories = await response.json();
        
        console.log('Subcategories received:', subcategories); // Debug log
        
        subcategories.forEach(subcategory => {
            const option = document.createElement('option');
            option.value = subcategory;
            option.textContent = subcategory;
            subcategorySelect.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading subcategories:', error);
    }
}

// Load types from API
async function loadTypes() {
    try {
        const response = await fetch('/Manager/GetTypes');
        const types = await response.json();
        
        const typeSelect = document.getElementById('type');
        typeSelect.innerHTML = '<option value="">Select Type</option>';
        
        types.forEach(type => {
            const option = document.createElement('option');
            option.value = type;
            option.textContent = type;
            typeSelect.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading types:', error);
    }
}

// Setup upload method tabs
function setupUploadTabs() {
    const tabs = document.querySelectorAll('.upload-tab');
    const methods = document.querySelectorAll('.upload-method');
    
    tabs.forEach(tab => {
        tab.addEventListener('click', function() {
            const method = this.dataset.method;
            
            // Update tab states
            tabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');
            
            // Update method visibility
            methods.forEach(m => m.classList.remove('active'));
            document.querySelector(`.${method}-method`).classList.add('active');
            
            // Clear the other method's input
            if (method === 'url') {
                document.getElementById('imageFile').value = '';
            } else {
                document.getElementById('itemimg').value = '';
            }
        });
    });
    
    // Handle file upload
    const fileInput = document.getElementById('imageFile');
    const fileInputCustom = document.getElementById('fileInputCustom');
    
    fileInput.addEventListener('change', function(e) {
        const file = e.target.files[0];
        if (file) {
            // Check file type
            if (!file.type.startsWith('image/')) {
                alert('Please select an image file.');
                return;
            }
            
            // Check file size (5MB max)
            if (file.size > 5 * 1024 * 1024) {
                alert('File size must be less than 5MB.');
                return;
            }
            
            // Preview image
            const reader = new FileReader();
            reader.onload = function(e) {
                document.getElementById('previewImg').src = e.target.result;
                document.getElementById('imagePreview').style.display = 'block';
                document.getElementById('imagePreview').style.opacity = '0';
                setTimeout(() => {
                    document.getElementById('imagePreview').style.transition = 'opacity 0.3s ease';
                    document.getElementById('imagePreview').style.opacity = '1';
                }, 10);
            };
            reader.readAsDataURL(file);
            
            // Update file input display
            const textElement = fileInputCustom.querySelector('.file-input-text');
            const subtextElement = fileInputCustom.querySelector('.file-input-subtext');
            textElement.textContent = file.name;
            subtextElement.textContent = `File selected: ${(file.size / 1024 / 1024).toFixed(2)} MB`;
            fileInputCustom.classList.add('has-file');
        }
    });

    // Drag and drop functionality
    if (fileInputCustom) {
        fileInputCustom.addEventListener('dragover', function(e) {
            e.preventDefault();
            this.classList.add('dragover');
        });
        
        fileInputCustom.addEventListener('dragleave', function(e) {
            e.preventDefault();
            this.classList.remove('dragover');
        });
        
        fileInputCustom.addEventListener('drop', function(e) {
            e.preventDefault();
            this.classList.remove('dragover');
            
            const files = e.dataTransfer.files;
            if (files.length > 0) {
                fileInput.files = files;
                fileInput.dispatchEvent(new Event('change'));
            }
        });
    }
}

// Remove image preview
function removePreview() {
    const imagePreview = document.getElementById('imagePreview');
    const fileInput = document.getElementById('imageFile');
    const fileInputCustom = document.getElementById('fileInputCustom');
    
    imagePreview.style.transition = 'opacity 0.3s ease';
    imagePreview.style.opacity = '0';
    setTimeout(() => {
        imagePreview.style.display = 'none';
    }, 300);
    
    fileInput.value = '';
    fileInputCustom.querySelector('.file-input-text').textContent = 'Choose an image file';
    fileInputCustom.querySelector('.file-input-subtext').textContent = 'PNG, JPG or JPEG (Max: 5MB)';
    fileInputCustom.classList.remove('has-file');
}

// Form validation
document.addEventListener('DOMContentLoaded', function() {
    const form = document.querySelector('.product-form');
    if (form) {
        form.addEventListener('submit', function(e) {
            const requiredFields = this.querySelectorAll('[required]');
            let isValid = true;
            
            // Check if image is provided (either URL or file)
            const imageUrl = document.getElementById('itemimg').value;
            const imageFile = document.getElementById('imageFile').files[0];
            
            if (!imageUrl && !imageFile) {
                isValid = false;
                alert('Please provide a product image (URL or file).');
                e.preventDefault();
                return;
            }
            
            requiredFields.forEach(field => {
                if (!field.value.trim()) {
                    isValid = false;
                    field.classList.add('error');
                } else {
                    field.classList.remove('error');
                }
            });
            
            if (!isValid) {
                e.preventDefault();
                alert('Please fill in all required fields.');
            }
        });
    }
});
