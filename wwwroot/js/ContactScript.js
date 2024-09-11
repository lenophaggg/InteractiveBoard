function clarifyPersonForContact(personTerm) {
    $.ajax({
        url: '/Contacts/ClarifyPerson',
        type: 'GET',
        data: { personTerm: personTerm },
        async: false,
        success: function (response) {
            $('#mainContainer').html(response);
        },
        error: function (xhr, status, error) {
            console.error('Произошла скибиди доп ес ес:');
            console.error('Статус запроса:', status);
            console.error('Текст ошибки:', error);
            console.error('Сообщение об ошибке:', xhr.responseText);
            alert('Произошла скибиди доп ес ес.');
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