// Автоматически добавляем токен в заголовок запроса
async function fetchWithAuth(url, options = {}) {
    const token = localStorage.getItem('jwtToken');
    if (token) {
        options.headers = options.headers || {};
        options.headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, options);
    return response;
}

// Функция для обновления описания директории
async function updateDescription(diskId) {
    const description = document.getElementById('descriptionInput').value;
    if (!description) {
        alert("Описание не может быть пустым.");
        return;
    }

    try {
        let response = await fetchWithAuth(`/api/disk/updateDescription/${diskId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(description)
        });

        if (response.ok) {
            alert("Описание успешно обновлено");
            loadConnectedDrives();
        } else {
            let message = await response.text();
            alert('Ошибка: ' + message);
        }
    } catch (error) {
        console.error('Ошибка при обновлении описания:', error);
    }
}

// Функция для редактирования описания для выбранного диска
function editDescription(diskId) {
    const descriptionInput = document.getElementById('descriptionInput');
    const diskRow = document.querySelector(`#diskRow${diskId}`);
    const description = diskRow.querySelector('.description').textContent;

    document.getElementById('modalOverlay').style.display = 'block'; // Показываем затемнённый фон
    
    document.getElementById('editDescriptionForm').style.display = 'block';// Отображаем форму редактирования
    descriptionInput.value = description;
    // Обработчик для кнопки "Отмена"
    document.getElementById('cancelEditDescription').onclick = function () {
        document.getElementById('editDescriptionForm').style.display = 'none'; // Скрываем форму
        document.getElementById('modalOverlay').style.display = 'none';
        document.getElementById('editDescriptionform').reset();
    };
    // Назначаем обработчик отправки формы
    document.getElementById('editDescriptionform').onsubmit = function (event) {
        event.preventDefault();
        updateDescription(diskId);
        document.getElementById('editDescriptionForm').style.display = 'none'; 
        document.getElementById('modalOverlay').style.display = 'none'; 
        document.getElementById('editDescriptionform').reset();
    };             
}

// Загрузка подключенных дисков
async function loadConnectedDrives() {
    try {
        let response = await fetchWithAuth('/api/disk/connectedDirectories');
        if (response.ok) {
            let drives = await response.json();
            const tableBody = document.querySelector('#connectedDirectoriesTable tbody');
            tableBody.innerHTML = '';

            drives.forEach(drive => {
                const row = document.createElement('tr');
                row.id = `diskRow${drive.id}`;
                row.innerHTML = `
                            <td>${drive.id}</td>
                            <td>${drive.name}</td>
                            <td>${drive.fileCount}</td>
                            <td>${drive.usedSpace}</td>
                            <td>${drive.freeSpace}</td>
                            <td class="description">${drive.description || 'Без описания'}</td>
                            <td>
                                <button id ="editDescriptionButton" onclick="editDescription(${drive.id})">Редактировать описание</button>
                                <button onclick="deleteDirectory('${drive.id}')">Удалить</button>
                            </td>
                        `;
                tableBody.appendChild(row);
            });
        } else {
            alert('Не удалось загрузить информацию о подключенных дисках');
        }
    } catch (error) {
        console.error('Ошибка при загрузке подключенных дисков:', error);
    }
}

// Удаление диска
async function deleteDirectory(driveId) {
    try {
        let response = await fetchWithAuth('/api/disk/deleteDirectory', {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(driveId)
        });

        if (response.ok) {
            alert('Диск успешно удален');
            loadConnectedDrives();
        } else {
            alert('Ошибка при удалении диска');
        }
    } catch (error) {
        console.error('Ошибка при удалении диска:', error);
    }
}

// Индексация файлов
async function indexFiles() {
    try {
        let response = await fetchWithAuth('/api/disk/index', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            let message = await response.text();
            alert(message);
        } else {
            let message = await response.text();
            alert('Ошибка: ' + message);
        }
    } catch (error) {
        console.error('Ошибка при индексации файлов:', error);
    }
}

// Добавление директории
async function addDirectory(directoryName) {
    if (!directoryName) {
        alert("Имя папки не может быть пустым.");
        return;
    }

    try {
        const response = await fetchWithAuth('/api/disk/addDirectory', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(directoryName),
        });

        if (response.ok) {
            alert("Папка успешно добавлена!");
            loadConnectedDrives();
        } else {
            const error = await response.text();
            alert(`Ошибка: ${error}`);
        }
    } catch (error) {
        console.error('Ошибка:', error);
        alert("Произошла ошибка при добавлении папки.");
    }
}
//Загрузка пользователей
async function loadUsers() {
    try {
        let response = await fetchWithAuth('/api/users');
        if (response.ok) {
            let users = await response.json();
            const tableBody = document.querySelector('#usersTable tbody');
            tableBody.innerHTML = '';

            users.forEach(user => {
                const row = document.createElement('tr');
                row.innerHTML = `
                            <td>${user.id}</td>
                            <td>${user.username}</td>
                            <td>${user.isAdmin ? "Admin": "User"}</td>
                            <td>
                                <button onclick="showChangePasswordForm(${user.id})">Сменить пароль</button>
                                <button onclick="deleteUser(${user.id})">Удалить</button>
                            </td>
                        `;
                tableBody.appendChild(row);
            });
        } else {
            alert('Не удалось загрузить список пользователей');
        }
    } catch (error) {
        console.error('Ошибка при загрузке пользователей:', error);
    }
}
//Удаление пользователей
async function deleteUser(userId) {
    try {
        let response = await fetchWithAuth(`/api/users/delete/${userId}`, {
            method: 'DELETE',
        });

        if (response.ok) {
            alert('Пользователь успешно удален');
            loadUsers();
        } else {
            alert('Ошибка при удалении пользователя');
        }
    } catch (error) {
        console.error('Ошибка при удалении пользователя:', error);
    }
}

//Обработка окна смены пароля
function showChangePasswordForm(userId) {
    document.getElementById('userIdForPassword').value = userId;
    document.getElementById('changePasswordForm').style.display = 'block';
    document.getElementById('modalOverlay').style.display = 'block';
    document.getElementById('cancelChangePassword').addEventListener('click', function () {
        document.getElementById('changePasswordForm').style.display = 'none';
        document.getElementById('modalOverlay').style.display = 'none';
    });
}

//Обновление пароля пользователя
document.getElementById('submitChangePassword').addEventListener('click', async function () {
    const userId = document.getElementById('userIdForPassword').value;
    const newPassword = document.getElementById('newPasswordForUser').value;
    try {
        let response = await fetchWithAuth(`/api/users/changePassword/${userId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ newPassword }),
        });
        if (response.ok) {
            alert('Пароль успешно изменен');
            document.getElementById('changePasswordForm').style.display = 'none';
            document.getElementById('modalOverlay').style.display = 'none';
            resetUserFields()
        } else {
            alert('Ошибка при изменении пароля', response);
        }
    } catch (error) {
        console.error('Ошибка при изменении пароля:', error);
    }
});

// Обработка кнопок добавления пользователя
document.getElementById('addUserButton').addEventListener('click', function () {
    document.getElementById('addUserForm').style.display = 'block';
    document.getElementById('modalOverlay').style.display = 'block';
});

document.getElementById('cancelAddUser').addEventListener('click', function () {
    document.getElementById('addUserForm').style.display = 'none';
    document.getElementById('modalOverlay').style.display = 'none';
    resetUserFields()
});
//Добавление пользователя
document.getElementById('submitAddUser').addEventListener('click', async function () {
    const username = document.getElementById('newUsername').value;
    const password = document.getElementById('newPassword').value;
    const email = document.getElementById('newEmail').value;
    const role = document.getElementById('newRole').value;
    try {
        let response = await fetchWithAuth('/api/users/add', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ username, password, role, email }),
        });
        if (response.ok) {
            alert('Пользователь успешно добавлен');
            document.getElementById('addUserForm').style.display = 'none';
            document.getElementById('modalOverlay').style.display = 'none';
            resetUserFields()
            loadUsers();
        } 
        else {
            
            alert('Ошибка при добавлении пользователя', response);
            
        }
    } catch (error) {
        console.error('Ошибка при добавлении пользователя:', error);
    }
});



//Удаление токена из куки
document.getElementById('logoutButton').addEventListener('click', function () {
    fetch('/api/auth/logout', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    })
        .then(response => response.json())
        .then(data => {
            console.log(data.message);
            window.location.reload(); // Перезагрузка страницы после выхода
        })
        .catch(error => {
            console.error("Ошибка при выходе:", error);
        });
});

document.getElementById('addDrivesButton').addEventListener('click', function () {
    document.getElementById('addDrivesForm').style.display = 'block';
    document.getElementById('modalOverlay').style.display = 'block';
    document.getElementById('cancelAddDrives').addEventListener('click', function () {
        document.getElementById('addDrivesForm').style.display = 'none';
        document.getElementById('modalOverlay').style.display = 'none';
    });
});


// Управление директориями
document.getElementById('addDirectoryButton').addEventListener('click', function () {
    const directoryName = document.getElementById('directoryNameInput').value;
    addDirectory(directoryName);
});


function resetUserFields() {
    document.getElementById('newUsername').value = "";
    document.getElementById('newEmail').value = "";
    document.getElementById('newPassword').value = "";
    document.getElementById('userIdForPassword').value = "";
    document.getElementById('newPasswordForUser').value = "";
    document.getElementById('newPassword').type = "text";
    document.getElementById('newPasswordForUser').type = "text";
    document.getElementById('newPassword').type = "password";
    document.getElementById('newPasswordForUser').type = "password";
}

// Функция для получения данных с сервера
async function fetchSystemInfo() {
    try {
        const response = await fetchWithAuth('/api/disk/generalDirectoriesInfo');
        if (!response.ok) {
            throw new Error('Ошибка при получении данных о системе');
        }
        const data = await response.json();
        updateSystemInfo(data);
        renderDiskUsageChart(data);
    } catch (error) {
        console.error('Ошибка:', error);
    }
}

// Функция для обновления текстовой информации
function updateSystemInfo(data) {
    document.getElementById('disk-count').textContent = data.diskCount;
    document.getElementById('general-free-space').textContent = `${data.generalFreeSpace.toFixed(2)} GB`;
    document.getElementById('general-file-count').textContent = data.generalFileCount;
    document.getElementById('general-disk-space').textContent = `${data.generalDiskSpace.toFixed(2)} GB`;
}

// Функция для рендеринга "графика" (линейная диаграмма)
function renderDiskUsageChart(data) {
    const totalSpace = data.generalDiskSpace;
    const freeSpace = data.generalFreeSpace;
    const usedSpace = totalSpace - freeSpace;

    // Вычисляем процент использования
    const usedPercent = (usedSpace / totalSpace) * 100;
    const freePercent = (freeSpace / totalSpace) * 100;

    // Обновляем стили для графика
    document.getElementById('used-space-bar').style.width = `${usedPercent}%`;
    document.getElementById('free-space-bar').style.width = `${freePercent}%`;

    // Добавляем подписи
    document.getElementById('used-space-label').textContent = `${usedSpace.toFixed(2)} GB (${usedPercent.toFixed(1)}%)`;
    document.getElementById('free-space-label').textContent = `${freeSpace.toFixed(2)} GB (${freePercent.toFixed(1)}%)`;
}




document.addEventListener('DOMContentLoaded', function () {
    fetchSystemInfo();
    loadConnectedDrives();
    loadUsers();
});