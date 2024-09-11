function getSchedule(searchTerm) {
    var isNumeric = !isNaN(parseFloat(searchTerm)) && isFinite(searchTerm);

    if (isNumeric) {
        // Выполнить действия для числа (номера группы)
        var facultyName = getFacultyFolderName(searchTerm);
        openScheduleByGroup(searchTerm, facultyName);
    } else {
        // Выполнить действия для набора слов (ФИО преподавателя)
        clarifyPersonForSchedule(searchTerm);
    }
}

function openScheduleByGroup(groupNumber, facultyName) {
    $.ajax({
        url: '/Schedule/GetScheduleByGroup', // Изменен URL вызываемого метода контроллера
        type: 'GET',
        data: { groupNumber: groupNumber, facultyName: facultyName }, // Параметры запроса изменены
        success: function (response) {
            $('#mainContainer').html(response);
        },
        error: function () {
            alert('Произошла ошибка при загрузке расписания.');
        }
    });
}

function openPersonSchedule(personName) {
    $.ajax({
        url: '/Schedule/GetScheduleByPerson',
        type: 'GET',
        data: { personName: personName },
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

function openModalInfo(personName) {
    $.ajax({
        url: '/Schedule/GetScheduleByPerson',
        type: 'GET',
        data: { personName: personName },
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

