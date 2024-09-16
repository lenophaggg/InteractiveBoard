function openModal(postId) {
    // Выполняем запрос на сервер, передавая id поста
    $.ajax({
        url: '/Home/GetPostData',
        type: 'GET',
        data: { postId: postId},
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

function openModalPDF(documentPath, nameDoc) {
    $.ajax({
        url: '/Home/GetDocument',
        type: 'GET',
        data: { directoryPath: documentPath, directoryName: nameDoc },
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

//document.addEventListener("DOMContentLoaded", function () {
//    // Проверьте наличие видео iframe и его инициализацию
//    var iframes = document.querySelectorAll('iframe');
//    if (iframes.length > 0) {
//        iframes.forEach(function (iframe) {
//            if (iframe.src.includes("youtube.com") && !iframe.id) {
//                console.error("Error: YouTube player element ID required.");
//                iframe.id = "youtube-player-" + Math.random().toString(36).substr(2, 9); // Генерация уникального ID
//            }
//        });
//    }
//});
