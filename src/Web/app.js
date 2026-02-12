const DATA_URLS = {
    user1: 'https://raw.githubusercontent.com/dienomb/MiBiblioteca/main/data/books.json',
    user2: 'https://raw.githubusercontent.com/dienomb/MiBiblioteca/main/data/books2.json'
};
const DATA_BASE_URL = 'https://raw.githubusercontent.com/dienomb/MiBiblioteca/main/data/';

let booksByUser = { user1: [], user2: [] };
let allBooks = [];
let currentSort = { field: 'FirstSeen', asc: true };
let currentUserFilter = 'all';

// Format date to Spanish locale
function formatDate(dateString) {
    if (!dateString) return 'Sin fecha';
    const date = new Date(dateString);
    return date.toLocaleDateString('es-ES', {
        day: 'numeric',
        month: 'long',
        year: 'numeric'
    });
}

// Calculate days until due date
function getDaysUntil(dateString) {
    if (!dateString) return null;
    const due = new Date(dateString);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    due.setHours(0, 0, 0, 0);
    const diffTime = due - today;
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return diffDays;
}

// Get status class based on due date
function getStatusClass(daysUntil) {
    if (daysUntil === null) return '';
    if (daysUntil < 0) return 'overdue';
    if (daysUntil <= 3) return 'due-soon';
    return '';
}

// Get status text based on due date
function getStatusText(daysUntil) {
    if (daysUntil === null) return '';
    if (daysUntil < 0) return `Vencido hace ${Math.abs(daysUntil)} dias`;
    if (daysUntil === 0) return 'Vence hoy';
    if (daysUntil === 1) return 'Vence manana';
    if (daysUntil <= 3) return `Vence en ${daysUntil} dias`;
    return `Vence en ${daysUntil} dias`;
}

// Create book card HTML
function createBookCard(book) {
    const daysUntil = getDaysUntil(book.DueDate);
    const statusClass = getStatusClass(daysUntil);
    const statusText = getStatusText(daysUntil);
    const formattedDueDate = formatDate(book.DueDate);
    const formattedFirstSeen = formatDate(book.FirstSeen);

    const author = book.Author ? escapeHtml(book.Author) : null;
    const coleccion = book.Coleccion ? escapeHtml(book.Coleccion) : null;
    const rawImageUrl = book.ImageUrl
        ? (book.ImageUrl.startsWith('http') ? book.ImageUrl : DATA_BASE_URL + book.ImageUrl)
        : null;
    const imageUrl = rawImageUrl ? escapeHtml(rawImageUrl) : null;

    const userLabel = book._user === 'user1' ? 'Usuario 1' : 'Usuario 2';
    const userBadgeClass = book._user === 'user1' ? 'user-badge-1' : 'user-badge-2';

    return `
        <div class="book-card ${statusClass}">
            ${imageUrl
                ? `<img class="book-cover" src="${imageUrl}" alt="${escapeHtml(book.Title)}" onclick="openLightbox(this.src, this.alt)">`
                : ''}
            <div class="book-info">
                <div class="book-title-row">
                    <h3 class="book-title">${escapeHtml(book.Title)}</h3>
                    <span class="user-badge ${userBadgeClass}">${userLabel}</span>
                </div>
                ${author ? `<p class="book-author">\u270d\ufe0f ${author}</p>` : ''}
                ${coleccion ? `<p class="book-coleccion">\ud83d\udcd6 ${coleccion}</p>` : ''}
                <div class="book-meta">
                    <div class="book-meta-item">
                        <span>\ud83d\udcc5</span>
                        <span class="due-date ${statusClass}">
                            ${formattedDueDate}
                            ${statusText ? `<br><small>${statusText}</small>` : ''}
                        </span>
                    </div>
                    <div class="book-meta-item">
                        <span>\ud83d\udc41\ufe0f</span>
                        <span>Visto: ${formattedFirstSeen}</span>
                    </div>
                </div>
            </div>
        </div>
    `;
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Lightbox
function openLightbox(src, alt) {
    const lightbox = document.getElementById('lightbox');
    const img = document.getElementById('lightboxImg');
    img.src = src;
    img.alt = alt || '';
    lightbox.classList.add('active');
    document.body.style.overflow = 'hidden';
}

function closeLightbox() {
    const lightbox = document.getElementById('lightbox');
    lightbox.classList.remove('active');
    document.body.style.overflow = '';
}

// Sort books by a given field
function sortBooks(books) {
    const { field, asc } = currentSort;
    return [...books].sort((a, b) => {
        let valA, valB;
        if (field === 'DueDate' || field === 'FirstSeen') {
            valA = a[field] ? new Date(a[field]).getTime() : 0;
            valB = b[field] ? new Date(b[field]).getTime() : 0;
        } else {
            valA = (a[field] || '').toLowerCase();
            valB = (b[field] || '').toLowerCase();
        }
        if (valA < valB) return asc ? -1 : 1;
        if (valA > valB) return asc ? 1 : -1;
        return 0;
    });
}

// Update sort button UI
function updateSortButtons() {
    document.querySelectorAll('.sort-btn').forEach(btn => {
        const field = btn.dataset.sort;
        btn.classList.toggle('active', field === currentSort.field);
        const arrow = btn.querySelector('.sort-arrow');
        if (field === currentSort.field) {
            arrow.textContent = currentSort.asc ? ' \u25B2' : ' \u25BC';
        } else {
            arrow.textContent = '';
        }
    });
}

// Get books for the current user filter
function getFilteredByUser() {
    if (currentUserFilter === 'all') return allBooks;
    return booksByUser[currentUserFilter] || [];
}

// Filter books by search term (title, author, coleccion)
function filterBooks(searchTerm) {
    const books = getFilteredByUser();
    const term = searchTerm.toLowerCase().trim();
    if (!term) return books;

    return books.filter(book =>
        book.Title.toLowerCase().includes(term) ||
        (book.Author && book.Author.toLowerCase().includes(term)) ||
        (book.Coleccion && book.Coleccion.toLowerCase().includes(term))
    );
}

// Render books to the page
function renderBooks(books) {
    const bookList = document.getElementById('bookList');
    const statsText = document.getElementById('statsText');

    if (books.length === 0) {
        bookList.innerHTML = `
            <div class="empty-state">
                <h3>\ud83d\udcda No se encontraron libros</h3>
                <p>No hay libros que coincidan con tu busqueda.</p>
            </div>
        `;
        statsText.textContent = 'No se encontraron libros';
        return;
    }

    // Count books by status
    let overdue = 0;
    let dueSoon = 0;

    books.forEach(book => {
        const daysUntil = getDaysUntil(book.DueDate);
        if (daysUntil !== null) {
            if (daysUntil < 0) overdue++;
            else if (daysUntil <= 3) dueSoon++;
        }
    });

    // Update stats
    let stats = `${books.length} libro${books.length !== 1 ? 's' : ''}`;
    if (overdue > 0) stats += ` \u00b7 ${overdue} vencido${overdue !== 1 ? 's' : ''}`;
    if (dueSoon > 0) stats += ` \u00b7 ${dueSoon} proximo${dueSoon !== 1 ? 's' : ''} a vencer`;
    statsText.textContent = stats;

    // Sort and render book cards
    const sorted = sortBooks(books);
    bookList.innerHTML = sorted.map(book => createBookCard(book)).join('');
}

// Fetch and display last update time
async function updateLastUpdateTime() {
    try {
        const response = await fetch('https://api.github.com/repos/dienomb/MiBiblioteca/commits?path=data/books.json&page=1&per_page=1');
        const commits = await response.json();

        if (commits && commits.length > 0) {
            const lastCommit = commits[0];
            const date = new Date(lastCommit.commit.author.date);
            const formatted = date.toLocaleDateString('es-ES', {
                day: 'numeric',
                month: 'long',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
            document.getElementById('lastUpdate').textContent = `Ultima actualizacion: ${formatted}`;
        }
    } catch (error) {
        console.error('Error fetching last update time:', error);
    }
}

// Fetch a single user's books, returning [] on 404
async function fetchUserBooks(url) {
    const response = await fetch(url);
    if (!response.ok) {
        if (response.status === 404) return [];
        throw new Error(`HTTP error! status: ${response.status}`);
    }
    return await response.json();
}

// Load books from both users
async function loadBooks() {
    const bookList = document.getElementById('bookList');
    bookList.innerHTML = `
        <div class="loading">
            <div class="spinner"></div>
            <p>Cargando libros...</p>
        </div>
    `;

    try {
        const [user1Books, user2Books] = await Promise.all([
            fetchUserBooks(DATA_URLS.user1),
            fetchUserBooks(DATA_URLS.user2)
        ]);

        booksByUser.user1 = user1Books.map(b => ({ ...b, _user: 'user1' }));
        booksByUser.user2 = user2Books.map(b => ({ ...b, _user: 'user2' }));
        allBooks = [...booksByUser.user1, ...booksByUser.user2];

        refresh();
        updateLastUpdateTime();

    } catch (error) {
        console.error('Error loading books:', error);
        bookList.innerHTML = `
            <div class="error">
                <h3>\u274c Error al cargar los libros</h3>
                <p>No se pudieron cargar los libros. Por favor, intentalo de nuevo mas tarde.</p>
                <p><small>Error: ${escapeHtml(error.message)}</small></p>
            </div>
        `;
    }
}

// Re-render with current search and sort
function refresh() {
    const searchInput = document.getElementById('searchInput');
    const filtered = filterBooks(searchInput.value);
    renderBooks(filtered);
    updateSortButtons();
}

// Initialize the app
document.addEventListener('DOMContentLoaded', () => {
    const searchInput = document.getElementById('searchInput');

    // Search functionality
    searchInput.addEventListener('input', () => refresh());

    // Sort button clicks
    document.querySelectorAll('.sort-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const field = btn.dataset.sort;
            if (currentSort.field === field) {
                currentSort.asc = !currentSort.asc;
            } else {
                currentSort.field = field;
                currentSort.asc = true;
            }
            refresh();
        });
    });

    // User tab clicks
    document.querySelectorAll('.user-tab').forEach(tab => {
        tab.addEventListener('click', () => {
            const user = tab.dataset.user;
            if (user === currentUserFilter) return;
            currentUserFilter = user;
            document.querySelectorAll('.user-tab').forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            refresh();
        });
    });

    // Close lightbox with Escape key
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeLightbox();
    });

    // Load books on page load
    loadBooks();
});
