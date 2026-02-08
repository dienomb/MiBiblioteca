const DATA_URL = 'https://raw.githubusercontent.com/dienomb/MiBiblioteca/main/data/books.json';

let allBooks = [];

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
    if (daysUntil < 0) return `Vencido hace ${Math.abs(daysUntil)} días`;
    if (daysUntil === 0) return 'Vence hoy';
    if (daysUntil === 1) return 'Vence mañana';
    if (daysUntil <= 3) return `Vence en ${daysUntil} días`;
    return `Vence en ${daysUntil} días`;
}

// Create book card HTML
function createBookCard(book) {
    const daysUntil = getDaysUntil(book.DueDate);
    const statusClass = getStatusClass(daysUntil);
    const statusText = getStatusText(daysUntil);
    const formattedDueDate = formatDate(book.DueDate);
    const formattedFirstSeen = formatDate(book.FirstSeen);

    return `
        <div class="book-card ${statusClass}">
            <h3 class="book-title">${escapeHtml(book.Title)}</h3>
            <div class="book-meta">
                <div class="book-meta-item">
                    <span>📅</span>
                    <span class="due-date ${statusClass}">
                        ${formattedDueDate}
                        ${statusText ? `<br><small>${statusText}</small>` : ''}
                    </span>
                </div>
                <div class="book-meta-item">
                    <span>👁️</span>
                    <span>Visto por primera vez: ${formattedFirstSeen}</span>
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

// Filter books by search term
function filterBooks(searchTerm) {
    const term = searchTerm.toLowerCase().trim();
    if (!term) return allBooks;

    return allBooks.filter(book =>
        book.Title.toLowerCase().includes(term)
    );
}

// Render books to the page
function renderBooks(books) {
    const bookList = document.getElementById('bookList');
    const statsText = document.getElementById('statsText');

    if (books.length === 0) {
        bookList.innerHTML = `
            <div class="empty-state">
                <h3>📚 No se encontraron libros</h3>
                <p>No hay libros que coincidan con tu búsqueda.</p>
            </div>
        `;
        statsText.textContent = 'No se encontraron libros';
        return;
    }

    // Count books by status
    const now = new Date();
    now.setHours(0, 0, 0, 0);
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
    if (overdue > 0) stats += ` · ${overdue} vencido${overdue !== 1 ? 's' : ''}`;
    if (dueSoon > 0) stats += ` · ${dueSoon} próximo${dueSoon !== 1 ? 's' : ''} a vencer`;
    statsText.textContent = stats;

    // Render book cards
    bookList.innerHTML = books.map(book => createBookCard(book)).join('');
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
            document.getElementById('lastUpdate').textContent = `Última actualización: ${formatted}`;
        }
    } catch (error) {
        console.error('Error fetching last update time:', error);
    }
}

// Load books from GitHub
async function loadBooks() {
    const bookList = document.getElementById('bookList');

    try {
        const response = await fetch(DATA_URL);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        allBooks = await response.json();

        // Sort by FirstSeen (oldest first)
        allBooks.sort((a, b) => new Date(a.FirstSeen) - new Date(b.FirstSeen));

        renderBooks(allBooks);
        updateLastUpdateTime();

    } catch (error) {
        console.error('Error loading books:', error);
        bookList.innerHTML = `
            <div class="error">
                <h3>❌ Error al cargar los libros</h3>
                <p>No se pudieron cargar los libros. Por favor, inténtalo de nuevo más tarde.</p>
                <p><small>Error: ${error.message}</small></p>
            </div>
        `;
    }
}

// Initialize the app
document.addEventListener('DOMContentLoaded', () => {
    const searchInput = document.getElementById('searchInput');

    // Search functionality
    searchInput.addEventListener('input', (e) => {
        const filtered = filterBooks(e.target.value);
        renderBooks(filtered);
    });

    // Load books on page load
    loadBooks();
});
