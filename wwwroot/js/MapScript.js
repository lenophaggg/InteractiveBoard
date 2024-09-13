let currentZoom = 1;  // Текущий масштаб
const zoomStep = 0.1;  // Шаг увеличения
const minZoom = 1;  // Минимальное значение зума
const maxZoom = 4;  // Максимальное значение зума

// Массив для хранения ссылок на SVG файлы
const svgFiles = {
    1: '/img/FloorPlans/1fl.svg',
    2: '/img/FloorPlans/2fl.svg',
    3: '/img/FloorPlans/3fl.svg',
    4: '/img/FloorPlans/4fl.svg',
    5: '/img/FloorPlans/5fl.svg'
};

// Переменные для перемещения и масштаба
let viewBoxX = 0, viewBoxY = 0, viewBoxWidth, viewBoxHeight;

// Функция для смены этажа
function changeFloor(floorNumber) {
    document.getElementById("floor-title").innerText = floorNumber + " этаж";

    fetch(svgFiles[floorNumber])
        .then(response => response.text())
        .then(svgContent => {
            const svgContainer = document.getElementById("svg-container");
            svgContainer.innerHTML = svgContent;

            const svgElement = svgContainer.querySelector("svg");

            // Получаем исходные размеры viewBox загруженного SVG
            const viewBox = svgElement.viewBox.baseVal;
            viewBoxWidth = viewBox.width;
            viewBoxHeight = viewBox.height;
            viewBoxX = viewBox.x;
            viewBoxY = viewBox.y;

            currentZoom = 1;  // Сбрасываем масштаб
            updateSvgViewBox();
        })
        .catch(error => console.error('Ошибка при загрузке SVG:', error));
}

// Функция для увеличения масштаба
function zoomIn() {
    if (currentZoom < maxZoom) {
        currentZoom += zoomStep;
        updateSvgViewBox();
    }
}

// Функция для уменьшения масштаба
function zoomOut() {
    if (currentZoom > minZoom) {
        currentZoom -= zoomStep;
        updateSvgViewBox();
    }
}

// Функция для обновления атрибута viewBox
function updateSvgViewBox() {
    const svgElement = document.getElementById("svg-container").querySelector("svg");
    if (svgElement) {
        // Масштабирование через изменение viewBox
        const newViewBoxWidth = viewBoxWidth / currentZoom;
        const newViewBoxHeight = viewBoxHeight / currentZoom;
        svgElement.setAttribute('viewBox', `${viewBoxX} ${viewBoxY} ${newViewBoxWidth} ${newViewBoxHeight}`);
    }
}

// Переменные для перемещения
let isDragging = false;
let startX, startY;

// Обработчик начала перемещения (клик мыши или касание)
function startDrag(event) {
    isDragging = true;
    startX = event.clientX || event.touches[0].clientX;
    startY = event.clientY || event.touches[0].clientY;
}

// Обработчик перемещения
function drag(event) {
    if (!isDragging) return;

    const currentX = event.clientX || event.touches[0].clientX;
    const currentY = event.clientY || event.touches[0].clientY;

    // Скорость перемещения уменьшена в зависимости от текущего масштаба
    const dx = (startX - currentX) / currentZoom;
    const dy = (startY - currentY) / currentZoom;

    viewBoxX += dx;
    viewBoxY += dy;

    updateSvgViewBox();

    startX = currentX;
    startY = currentY;
}

// Обработчик окончания перемещения
function endDrag() {
    isDragging = false;
}

// Добавляем слушатели событий на контейнер SVG
const svgContainer = document.getElementById("svg-container");
svgContainer.addEventListener("mousedown", startDrag);
svgContainer.addEventListener("touchstart", startDrag);

svgContainer.addEventListener("mousemove", drag);
svgContainer.addEventListener("touchmove", drag);

svgContainer.addEventListener("mouseup", endDrag);
svgContainer.addEventListener("touchend", endDrag);
svgContainer.addEventListener("mouseleave", endDrag);

// По умолчанию загружаем 1 этаж
changeFloor(1);
