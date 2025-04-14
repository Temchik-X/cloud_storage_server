// Обработчик формы логина
document.getElementById('loginForm').addEventListener('submit', async function (event) {
    event.preventDefault();
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (response.ok) {
            const result = await response.json();
            if (result.token) {
                localStorage.setItem('jwtToken', result.token);
                alert('Аутентификация успешна');
                window.location.reload(); // Перезагружаем страницу
            } else {
                alert('Не удалось получить токен');
            }
        } else {
            alert('Ошибка при входе');
        }
    } catch (error) {
        console.error('Ошибка при аутентификации:', error);
    }
});