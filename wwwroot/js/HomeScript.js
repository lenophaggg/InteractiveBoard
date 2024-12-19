$(document).ready(function () {
    // Инициализация слайдера для фотографий
    const imageCarousels = document.querySelectorAll('.image-carousel');
    imageCarousels.forEach(container => {
        const imagesBase64 = container.getAttribute('data-images-base64');
        if (!imagesBase64) return;

        const imagesJson = atob(imagesBase64);
        const images = JSON.parse(imagesJson);
        const img = container.querySelector('img');

        if (!img || !images || images.length === 0) return;

        let currentIndex = 0;

        // Устанавливаем начальное изображение
        img.src = images[currentIndex];
        img.style.width = '646px';
        img.style.height = container.getAttribute('data-height') + 'px';
        img.style.objectFit = 'cover';

        // Функция для смены изображений
        function showNextImage() {
            currentIndex = (currentIndex + 1) % images.length;
            img.src = images[currentIndex];
        }

        // Меняем изображение каждые 5 секунд
        setInterval(showNextImage, 3000);
    });
});

/**
 * Открывает модальное окно для поста.
 */
function openModal(postId) {
    $.ajax({
        url: '/Home/GetPostData',
        type: 'GET',
        data: { postId: postId },
        success: function (data) {
            const $modalBody = $('#myModal .modal-body');
            $modalBody.empty();
            $modalBody.html(data);
            $('#myModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке данных поста.');
        }
    });
}

/**
 * Открывает модальное окно для просмотра PDF-документа.
 */
function openModalPDF(documentPath, nameDoc) {
    $.ajax({
        url: '/Home/GetDocument',
        type: 'GET',
        data: { directoryPath: documentPath, directoryName: nameDoc },
        success: function (data) {
            const $modalBody = $('#myModal .modal-body');
            $modalBody.empty();
            $modalBody.html(data);
            $('#myModal').modal('show');
        },
        error: function () {
            alert('Произошла ошибка при загрузке документа.');
        }
    });
}
