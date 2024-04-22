
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

function getFacultyFolderName(groupNumber) {
    if (groupNumber.length == 4) {
        switch (groupNumber[0]) {
            case '1':
                return "shipbuilding_and_ocean_engineering";
            case '2':
                return "ship_power_engineering_and_automation";
            case '3':
                return "marine_instrument_engineering";
            case '4':
                return "engineering_and_economics";
            case '5':
            case '6':
            case '7':
                return "natural_sciences_and_humanities";
            case '8':
                return "college_of_SMTU";
        }
    } else if (groupNumber.length == 5 && groupNumber.startsWith("20")) {
        return "digital_industrial_technologies";
    }

    return "unknown_faculty";
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
