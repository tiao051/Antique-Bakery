// ProductList.js - Manager Product List Scripts
let productToDelete = '';

// Auto-hide alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.opacity = '0';
            setTimeout(() => {
                alert.remove();
            }, 300);
        }, 5000);
    });

    // Setup modal event listeners
    setupModalListeners();
});

// Setup modal event listeners
function setupModalListeners() {
    // Close modal when clicking outside
    const deleteModal = document.getElementById('deleteModal');
    if (deleteModal) {
        deleteModal.addEventListener('click', function(e) {
            if (e.target === this) {
                closeDeleteModal();
            }
        });
    }

    // Close modal with Escape key
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            closeDeleteModal();
        }
    });
}

// Show delete confirmation modal
function confirmDelete(itemName) {
    productToDelete = itemName;
    document.getElementById('deleteProductName').textContent = itemName;
    
    const modal = document.getElementById('deleteModal');
    modal.classList.add('active');
    
    // Focus management for accessibility
    setTimeout(() => {
        document.getElementById('confirmDeleteBtn').focus();
    }, 300);
}

// Close delete confirmation modal
function closeDeleteModal() {
    const modal = document.getElementById('deleteModal');
    modal.classList.remove('active');
    productToDelete = '';
    
    // Reset button state
    resetButtonState();
}

// Helper function to reset button state
function resetButtonState() {
    const confirmBtn = document.getElementById('confirmDeleteBtn');
    confirmBtn.classList.remove('loading');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = `
        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="3,6 5,6 21,6"></polyline>
            <path d="m19,6v14a2,2 0 0,1 -2,2H7a2,2 0 0,1 -2,-2V6m3,0V4a2,2 0 0,1 2,-2h4a2,2 0 0,1 2,2v2"></path>
            <line x1="10" y1="11" x2="10" y2="17"></line>
            <line x1="14" y1="11" x2="14" y2="17"></line>
        </svg>
        Yes, Delete
    `;
}

// Perform the actual delete operation
async function performDelete() {
    if (!productToDelete) return;
    
    const confirmBtn = document.getElementById('confirmDeleteBtn');
    
    // Show loading state
    confirmBtn.classList.add('loading');
    confirmBtn.disabled = true;
    confirmBtn.innerHTML = `
        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="3,6 5,6 21,6"></polyline>
            <path d="m19,6v14a2,2 0 0,1 -2,2H7a2,2 0 0,1 -2,-2V6m3,0V4a2,2 0 0,1 2,-2h4a2,2 0 0,1 2,2v2"></path>
            <line x1="10" y1="11" x2="10" y2="17"></line>
            <line x1="14" y1="11" x2="14" y2="17"></line>
        </svg>
        Deleting...
    `;
    
    try {
        const response = await fetch('/Manager/Delete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            },
            body: `itemName=${encodeURIComponent(productToDelete)}`
        });

        const result = await response.json();
        
        if (result.success) {
            // Show success message
            showAlert('success', result.message);
            
            // Remove the product card from UI
            const productCard = document.querySelector(`[data-product-name="${productToDelete}"]`);
            if (productCard) {
                productCard.style.transition = 'all 0.3s ease';
                productCard.style.transform = 'scale(0)';
                productCard.style.opacity = '0';
                
                setTimeout(() => {
                    productCard.remove();
                    
                    // Check if section is empty and update UI
                    checkEmptyState();
                }, 300);
            }
            
            closeDeleteModal();
        } else {
            showAlert('error', result.message);
            resetButtonState();
        }
    } catch (error) {
        console.error('Error deleting product:', error);
        showAlert('error', 'An error occurred while deleting the product.');
        resetButtonState();
    }
}

// Show alert message
function showAlert(type, message) {
    const alertContainer = document.querySelector('.content-wrapper');
    const alertElement = document.createElement('div');
    alertElement.className = `alert alert-${type}`;
    
    const icon = type === 'success' 
        ? `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
             <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
             <polyline points="22,4 12,14.01 9,11.01"></polyline>
           </svg>`
        : `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
             <circle cx="12" cy="12" r="10"></circle>
             <line x1="15" y1="9" x2="9" y2="15"></line>
             <line x1="9" y1="9" x2="15" y2="15"></line>
           </svg>`;
    
    alertElement.innerHTML = `${icon}${message}`;
    
    // Insert at the beginning of content wrapper
    const pageHeader = alertContainer.querySelector('.page-header');
    if (pageHeader) {
        alertContainer.insertBefore(alertElement, pageHeader.nextSibling);
    } else {
        alertContainer.insertBefore(alertElement, alertContainer.firstChild);
    }
    
    // Auto-hide after 5 seconds
    setTimeout(() => {
        alertElement.style.opacity = '0';
        setTimeout(() => {
            alertElement.remove();
        }, 300);
    }, 5000);
}

// Check if any sections are empty and update UI accordingly
function checkEmptyState() {
    let totalProductsRemaining = 0;
    
    // Check type sections first (innermost level)
    const typeSections = document.querySelectorAll('.type-section');
    typeSections.forEach(typeSection => {
        const productsGrid = typeSection.querySelector('.products-grid');
        const productCards = productsGrid ? productsGrid.children.length : 0;
        
        if (productCards === 0) {
            // Remove empty type section
            typeSection.style.transition = 'all 0.3s ease';
            typeSection.style.transform = 'scale(0.9)';
            typeSection.style.opacity = '0';
            typeSection.style.height = '0';
            typeSection.style.marginBottom = '0';
            typeSection.style.overflow = 'hidden';
            
            setTimeout(() => {
                typeSection.remove();
                checkEmptyParentSections();
            }, 300);
        } else {
            totalProductsRemaining += productCards;
        }
    });
    
    // If no products remain at all, show empty state
    if (totalProductsRemaining === 0) {
        setTimeout(() => {
            const productsContainer = document.querySelector('.products-container');
            if (productsContainer) {
                productsContainer.innerHTML = `
                    <div class="empty-state">
                        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"></path>
                            <line x1="7" y1="7" x2="7.01" y2="7"></line>
                        </svg>
                        <h3>No Products Found</h3>
                        <p>There are no products in your inventory yet.</p>
                        <a href="/Manager/CreateProduct" class="btn btn-primary">
                            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <line x1="12" y1="5" x2="12" y2="19"></line>
                                <line x1="5" y1="12" x2="19" y2="12"></line>
                            </svg>
                            Create First Product
                        </a>
                    </div>
                `;
            }
        }, 350);
    }
}

// Check and remove empty parent sections (subcategory and category)
function checkEmptyParentSections() {
    // Check subcategory sections
    const subcategorySections = document.querySelectorAll('.subcategory-section');
    subcategorySections.forEach(subcatSection => {
        const typeSections = subcatSection.querySelectorAll('.type-section');
        if (typeSections.length === 0) {
            // Remove empty subcategory section
            subcatSection.style.transition = 'all 0.3s ease';
            subcatSection.style.transform = 'scale(0.9)';
            subcatSection.style.opacity = '0';
            subcatSection.style.height = '0';
            subcatSection.style.marginBottom = '0';
            subcatSection.style.overflow = 'hidden';
            
            setTimeout(() => {
                subcatSection.remove();
                checkEmptyCategorySections();
            }, 300);
        }
    });
}

// Check and remove empty category sections
function checkEmptyCategorySections() {
    const categorySections = document.querySelectorAll('.category-section');
    categorySections.forEach(catSection => {
        const subcategorySections = catSection.querySelectorAll('.subcategory-section');
        if (subcategorySections.length === 0) {
            // Remove empty category section
            catSection.style.transition = 'all 0.3s ease';
            catSection.style.transform = 'scale(0.9)';
            catSection.style.opacity = '0';
            catSection.style.height = '0';
            catSection.style.marginBottom = '0';
            catSection.style.overflow = 'hidden';
            
            setTimeout(() => {
                catSection.remove();
            }, 300);
        }
    });
}
