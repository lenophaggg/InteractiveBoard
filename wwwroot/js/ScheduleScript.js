
function getSchedule(searchTerm) {
    // Проверка на пустую строку или строку, состоящую только из пробелов
    
    var isNumeric = !isNaN(parseFloat(searchTerm)) && isFinite(searchTerm);

    if (isNumeric) {
        // Выполнить действия для числа (номера группы)       
        openScheduleByGroup(searchTerm);
    } else {
        // Выполнить действия для набора слов (ФИО преподавателя)
        clarifyPersonForSchedule(searchTerm);
    }
}

function openScheduleByGroup(groupNumber) {
    $.ajax({
        url: '/Schedule/GetScheduleByGroup', // Изменен URL вызываемого метода контроллера
        type: 'GET',
        data: { groupNumber: groupNumber}, // Параметры запроса изменены
        success: function (response) {
            $('#mainContainer').html(response);
        },
        error: function () {
            alert('Произошла ошибка при загрузке расписания.');
        }
    });
}

function openPersonSchedule(personName, universityIdContact) {
    $.ajax({
        url: '/Schedule/GetScheduleByPerson',
        type: 'GET',
        data: { personName: personName, universityIdContact: universityIdContact },
        success: function (response) {
            $('#myModal').modal('hide');

            $('#mainContainer').html(response);
        },
        error: function (xhr, status, error) {
            console.error('Произошла ошибка при загрузке расписания:', error);
            console.error('Статус запроса:', status);
            console.error('Текст ошибки:', xhr.statusText);
            alert('Произошла ошибка при загрузке расписания.');
        }
    });
}

function clarifyPersonForSchedule(personTerm) {
    $.ajax({
        url: '/Schedule/ClarifyPerson',
        type: 'GET',
        data: { personTerm: personTerm },
        success: function (response) {
            $('#mainContainer').html(response);
        },
        error: function (xhr, status, error) {
            console.error('Произошла ошибка при загрузке расписания:', error);
            console.error('Статус запроса:', status);
            console.error('Текст ошибки:', xhr.statusText);
            alert('Произошла ошибка при загрузке расписания.');
        }
    });
}

function openModalInfo(personName, universityIdContact) {
    $.ajax({
        url: '/Contacts/GetContactPerson',
        type: 'GET',
        data: { personName: personName, universityIdContact: universityIdContact },
        success: function (data) {
            // Очищаем содержимое модального окна
            $('#myModal .modal-body').empty();

            // Вставляем PartialView в модальное окно
            $('#myModal .modal-body').html(data);

            // Открываем модальное окно
            $('#myModal').modal('show');
        },
        error: function (xhr, status, error) {
            console.error('Произошла ошибка при загрузке контакта:', error);
            console.error('Статус запроса:', status);
            console.error('Текст ошибки:', xhr.statusText);
            alert('Произошла ошибка при контакта.');
        }
    });
}
