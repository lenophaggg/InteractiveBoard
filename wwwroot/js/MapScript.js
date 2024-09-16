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

// Фиксированная точка маршрута (вход)
const entryPoint = { x: 100, y: 200 };

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

            // Обновляем обработчики кликов на кабинеты для маршрута
            addRouteClickListeners();
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
        const newViewBoxWidth = viewBoxWidth / currentZoom;
        const newViewBoxHeight = viewBoxHeight / currentZoom;
        svgElement.setAttribute('viewBox', `${viewBoxX} ${viewBoxY} ${newViewBoxWidth} ${newViewBoxHeight}`);
    }
}

// Функция для получения координат кабинета
function getPolygonCoordinates(polygonId) {
    const polygon = document.getElementById(polygonId);
    if (polygon) {
        const points = polygon.getAttribute('points');
        const coordinates = points.split(' ').map(point => point.split(',').map(Number));
        return coordinates;
    }
    return null;
}

// Функция для рисования маршрута
function drawRoute(startX, startY, endX, endY) {
    const svgElement = document.getElementById("svg-container").querySelector("svg");
    const line = document.createElementNS("http://www.w3.org/2000/svg", "line");
    line.setAttribute("x1", startX);
    line.setAttribute("y1", startY);
    line.setAttribute("x2", endX);
    line.setAttribute("y2", endY);
    line.setAttribute("stroke", "red");
    line.setAttribute("stroke-width", "2");
    svgElement.appendChild(line);
}

// Функция для добавления обработчиков кликов на кабинеты
function addRouteClickListeners() {
    // Ищем только полигоны с классом "cls-5"
    document.querySelectorAll('polygon.cls-5').forEach(polygon => {
        polygon.addEventListener('click', function () {
            const cabinetCoordinates = getPolygonCoordinates(this.id);
            if (cabinetCoordinates) {
                const [endX, endY] = cabinetCoordinates[0];  // Первая точка кабинета
                drawRoute(entryPoint.x, entryPoint.y, endX, endY);
            }
        });
    });
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
