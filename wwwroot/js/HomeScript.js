window.onload = function () {
    // Слайдер для фоток
    document.querySelectorAll('.image-carousel').forEach(container => {
        const imagesBase64 = container.getAttribute('data-images-base64');
        const imagesJson = atob(imagesBase64);
        const images = JSON.parse(imagesJson);
        const img = container.querySelector('img');

        let currentIndex = 0;

        // Устанавливаем начальное изображение с нужной высотой
        img.src = images[currentIndex];
        img.style.width = '646px';
        img.style.height = container.getAttribute('data-height') + 'px';
        img.style.objectFit = 'cover';

        // Функция для смены изображений
        function showNextImage() {
            currentIndex = (currentIndex + 1) % images.length; // Циклическая смена изображений
            img.src = images[currentIndex];
        }

        // Меняем изображение каждые 5 секунд
        setInterval(showNextImage, 3000);
    });
};

function openModal(postId) {
    // Открываем модальное окно для поста
    $.ajax({
        url: '/Home/GetPostData',
        type: 'GET',
        data: { postId: postId },
        success: function (data) {
            $('#myModal .modal-body').empty();
            $('#myModal .modal-body').html(data);
            $('#myModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке данных поста.');
        }
    });
}

function openModalPDF(documentPath, nameDoc) {
    // Открываем модальное окно для документа
    $.ajax({
        url: '/Home/GetDocument',
        type: 'GET',
        data: { directoryPath: documentPath, directoryName: nameDoc },
        success: function (data) {
            $('#docPreviewModal .modal-pdf-body').empty();
            $('#docPreviewModal .modal-pdf-body').html(data);
            $('#docPreviewModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке документа.');
        }
    });
}

function openModal(postId) {
    // Открываем модальное окно для поста
    $.ajax({
        url: '/Home/GetPostData',
        type: 'GET',
        data: { postId: postId },
        success: function (data) {
            $('#myModal .modal-body').empty();
            $('#myModal .modal-body').html(data);
            $('#myModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке данных поста.');
        }
    });
}

function openModalPDF(documentPath, nameDoc) {
    // Открываем модальное окно для документа
    $.ajax({
        url: '/Home/GetDocument',
        type: 'GET',
        data: { directoryPath: documentPath, directoryName: nameDoc },
        success: function (data) {
            $('#docPreviewModal .modal-pdf-body').empty();
            $('#docPreviewModal .modal-pdf-body').html(data);
            $('#docPreviewModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке документа.');
        }
    });
}
