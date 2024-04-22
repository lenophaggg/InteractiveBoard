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
    if (idleTime > 60) { // 30 секунд бездействия
        window.location.href = "/Inactive/Index"; // Перенаправление на указанный адрес
    }
}

$(document).on('mousemove keydown scroll', function () {
    idleTime = 0; // Сброс счетчика бездействия
});

document.addEventListener('touchstart', function () {
    idleTime = 0; // Сброс счетчика бездействия при касании экрана
});


