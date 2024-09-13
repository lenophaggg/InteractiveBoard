document.addEventListener('shown.bs.modal', function () {
    adjustModalBackdrop();
});

window.addEventListener('resize', function () {
    adjustModalBackdrop();
});

function adjustModalBackdrop() {
    const modalBackdrop = document.querySelector('.modal-backdrop');
    if (modalBackdrop) {
        modalBackdrop.style.width = window.innerWidth + 'px';
        modalBackdrop.style.height = window.innerHeight + 'px';
    }
}

function updateDateTime() {
    var now = new Date();
    var days = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];
    var months = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября',
        'ноября', 'декабря'];
    var day = days[now.getDay()];
    var date = now.getDate();
    var month = months[now.getMonth()];
    var hours = now.getHours();
    var minutes = now.getMinutes();
    if (minutes < 10) minutes = '0' + minutes;

    // Обновление времени
    document.getElementById('time').innerText = hours + ':' + minutes;

    // Обновление даты
    document.getElementById('date').innerText = day + ', ' + date + ' ' + month;
} setInterval(updateDateTime, 1000);


var idleTime = 0;
var idleInterval = setInterval(timerIncrement, 1000);

function timerIncrement() {
    idleTime++;
    if (idleTime > 90) { // 30 секунд бездействия
        window.location.href = "/Inactive/Index"; // Перенаправление на указанный адрес
    }
}

$(document).on('mousemove keydown scroll', function () {
    idleTime = 0; // Сброс счетчика бездействия
});

document.addEventListener('touchstart', function () {
    idleTime = 0; // Сброс счетчика бездействия при касании экрана
});

$(document).ready(function () {
    $('#carouselExampleIndicators').carousel({
        interval: 3000,
        ride: 'carousel'
    });

    $('#carouselExampleIndicators').on('slide.bs.carousel', function (e) {
        var $e = $(e.relatedTarget);
        var idx = $e.index();
        var itemsPerSlide = 2;
        var totalItems = $('.carousel-item').length;

        if (idx >= totalItems - (itemsPerSlide - 1)) {
            var it = itemsPerSlide - (totalItems - idx);
            for (var i = 0; i < it; i++) {
                // Append slides to end
                if (e.direction == "left") {
                    $('.carousel-item').eq(i).appendTo('.carousel-inner');
                } else {
                    $('.carousel-item').eq(0).appendTo('.carousel-inner');
                }
            }
        }
    });
});
