document.addEventListener('DOMContentLoaded', function () {
    // 1. FAQ Accordion Logic
    const faqItems = document.querySelectorAll('.faq-accordion-item');
    
    faqItems.forEach(item => {
        const header = item.querySelector('.faq-header');
        header.addEventListener('click', () => {
            const isActive = item.classList.contains('active');
            
            // Close all other items
            faqItems.forEach(otherItem => {
                if (otherItem !== item) {
                    otherItem.classList.remove('active');
                    const otherBody = otherItem.querySelector('.faq-body');
                    if (otherBody) otherBody.style.maxHeight = null;
                }
            });

            // Toggle current item
            item.classList.toggle('active');
            const body = item.querySelector('.faq-body');
            if (item.classList.contains('active')) {
                body.style.maxHeight = body.scrollHeight + "px";
            } else {
                body.style.maxHeight = null;
            }
        });
    });


    // 2. FAQ Search Logic
    const searchInput = document.getElementById('faqSearch');
    const noResults = document.getElementById('noResults');

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            const searchTerm = this.value.toLowerCase().trim();
            let hasMatches = false;

            faqItems.forEach(item => {
                const question = item.querySelector('span').innerText.toLowerCase();
                const answer = item.querySelector('.faq-body-content').innerText.toLowerCase();

                if (question.includes(searchTerm) || answer.includes(searchTerm)) {
                    item.style.display = 'block';
                    hasMatches = true;
                } else {
                    item.style.display = 'none';
                }
            });

            // Show/Hide No Results UI
            if (noResults) {
                noResults.style.display = hasMatches ? 'none' : 'block';
            }
        });
    }

    // 3. Floating Help Button / Popup Logic
    const helpBtn = document.getElementById('floatingHelpBtn');
    const helpPopup = document.getElementById('helpPopup');
    const closePopupBtn = document.getElementById('closeHelpPopup');

    if (helpBtn && helpPopup) {
        helpBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const isVisible = helpPopup.style.display === 'block';
            helpPopup.style.display = isVisible ? 'none' : 'block';
        });

        if (closePopupBtn) {
            closePopupBtn.addEventListener('click', () => {
                helpPopup.style.display = 'none';
            });
        }

        // Close popup if clicking outside
        document.addEventListener('click', (e) => {
            if (!helpPopup.contains(e.target) && e.target !== helpBtn) {
                helpPopup.style.display = 'none';
            }
        });
    }
});
