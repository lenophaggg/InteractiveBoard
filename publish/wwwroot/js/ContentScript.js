function openModal(postId) {
    // Выполняем запрос на сервер, передавая id поста
    $.ajax({
        url: '/Home/GetPostData',
        type: 'GET',
        data: { postId: postId },
        success: function (data) {
            // Очищаем содержимое модального окна
            $('#myModal .modal-body').empty();

            // Вставляем PartialView в модальное окно
            $('#myModal .modal-body').html(data);

            // Открываем модальное окно
            $('#myModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке данных поста.');
        }
    });
}

function openModalPDF(documentPath) {
    $.ajax({
        url: '/Home/GetDocument',
        type: 'GET',
        data: { documentPath: documentPath },
        success: function (data) {
            // Очищаем содержимое модального окна
            $('#docPreviewModal .modal-pdf-body').empty();

            // Вставляем PartialView в модальное окно
            $('#docPreviewModal .modal-pdf-body').html(data);

            // Открываем модальное окно
            $('#docPreviewModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке документа.');
        }
    });
}

