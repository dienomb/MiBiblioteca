# Madrid Library CSS Selectors

**Instructions:** Fill in the actual CSS selectors you discover by inspecting the website.

## Login Form
- Username field: `FILL_IN_HERE` (e.g., `#username`, `input[name='usuario']`)
- Password field: `FILL_IN_HERE` (e.g., `#password`, `input[name='clave']`)
- Submit button: `FILL_IN_HERE` (e.g., `button[type='submit']`, `input[value='Entrar']`)

## Navigation Path (3 clicks to reach book list)
1. Click 1: `FILL_IN_HERE` (e.g., `a:has-text("Mi cuenta")`, `a[href*='cuenta']`)
2. Click 2: `FILL_IN_HERE` (e.g., `a:has-text("Reservas")`, `a[href*='reservas']`)
3. Click 3: `FILL_IN_HERE` (e.g., `a:has-text("Ver todas")`, `button.ver-todas`)

## Book List Structure
- Container selector: `FILL_IN_HERE` (e.g., `table#reservas`, `.reservation-list`)
- Each book row: `FILL_IN_HERE` (e.g., `tr.libro`, `.book-item`)
- Title within row: `FILL_IN_HERE` (e.g., `td.titulo`, `.book-title`)
- Author within row: `FILL_IN_HERE` (e.g., `td.autor`, `.book-author`)

## Testing Selectors

To test a selector in the browser console:
```javascript
// Test if selector exists
document.querySelector('your-selector-here')

// Get all matching elements
document.querySelectorAll('your-selector-here')

// Get text content
document.querySelector('your-selector-here')?.textContent
```

## Notes

Add any additional notes about the website structure:
- Are there multiple pages of books?
- Are there any CAPTCHA or security measures?
- Does the site use iframes?
- Any JavaScript-heavy interactions?
