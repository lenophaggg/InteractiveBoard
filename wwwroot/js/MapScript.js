let currentZoom = 1;
const zoomStep = 0.1;
const minZoom = 1;
const maxZoom = 4;

// Массив ссылок на SVG-файлы
const svgFiles = {
    1: '/img/FloorPlans/1fl.svg',
    2: '/img/FloorPlans/2fl.svg',
    3: '/img/FloorPlans/3fl.svg',
    4: '/img/FloorPlans/4fl.svg',
    5: '/img/FloorPlans/5fl.svg'
};

// Переменные для viewBox
let viewBoxX = 0, viewBoxY = 0, viewBoxWidth, viewBoxHeight;

// Переменные для перемещения
let isDragging = false;
let startX, startY;

// Функция смены этажа
function changeFloor(floorNumber) {
    document.getElementById("floor-title").innerText = floorNumber + " этаж";

    fetch(svgFiles[floorNumber])
        .then(response => response.text())
        .then(svgContent => {
            const svgContainer = document.getElementById("svg-container");
            svgContainer.innerHTML = svgContent;  // Загружаем SVG в контейнер

            const svgElement = svgContainer.querySelector("svg");

            // Инициализация viewBox
            const viewBox = svgElement.viewBox.baseVal;
            viewBoxWidth = viewBox.width;
            viewBoxHeight = viewBox.height;
            viewBoxX = viewBox.x;
            viewBoxY = viewBox.y;

            currentZoom = 1;
            updateSvgViewBox();

            // Привязка событий перемещения и масштабирования
            svgElement.addEventListener("mousedown", startDrag);
            svgElement.addEventListener("mousemove", drag);
            svgElement.addEventListener("mouseup", endDrag);
            svgElement.addEventListener("mouseleave", endDrag);

            svgElement.addEventListener("touchstart", startDrag);
            svgElement.addEventListener("touchmove", drag);
            svgElement.addEventListener("touchend", endDrag);

            // Построение маршрута
            buildRoute();
        })
        .catch(error => console.error('Ошибка при загрузке SVG:', error));
}

// Функции зума
function zoomIn() {
    if (currentZoom < maxZoom) {
        currentZoom += zoomStep;
        updateSvgViewBox();
    }
}

function zoomOut() {
    if (currentZoom > minZoom) {
        currentZoom -= zoomStep;
        updateSvgViewBox();
    }
}

function updateSvgViewBox() {
    const svgElement = document.querySelector("#svg-container svg");
    if (svgElement) {
        const newViewBoxWidth = viewBoxWidth / currentZoom;
        const newViewBoxHeight = viewBoxHeight / currentZoom;
        svgElement.setAttribute('viewBox', `${viewBoxX} ${viewBoxY} ${newViewBoxWidth} ${newViewBoxHeight}`);
    }
}

// Функции перемещения
function startDrag(event) {
    isDragging = true;
    startX = event.clientX || event.touches[0].clientX;
    startY = event.clientY || event.touches[0].clientY;
}

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

function endDrag() {
    isDragging = false;
}

// Функция построения маршрута
function buildRoute() {
    const svg = document.querySelector('#svg-container svg');

    // Получаем начальную точку и полигон кабинета 443
    const startCircle = svg.querySelector('circle.current-position');
    const room443 = svg.querySelector('polygon.cls-5');

    if (!startCircle || !room443) {
        console.error("Не удалось найти начальную точку или кабинет 443.");
        return;
    }

    // Извлекаем координаты начальной точки
    const startX = parseFloat(startCircle.getAttribute('cx'));
    const startY = parseFloat(startCircle.getAttribute('cy'));

    // Извлекаем координаты полигона кабинета 443
    const roomPoints = room443.getAttribute('points').split(' ').map(point => {
        const [x, y] = point.split(',');
        return { x: parseFloat(x), y: parseFloat(y) };
    });

    // Находим центр полигона кабинета 443
    const targetX = roomPoints.reduce((sum, p) => sum + p.x, 0) / roomPoints.length;
    const targetY = roomPoints.reduce((sum, p) => sum + p.y, 0) / roomPoints.length;

    // Построение маршрута
    const pathElement = document.createElementNS("http://www.w3.org/2000/svg", "path");
    pathElement.setAttribute("class", "path");

    // Строим путь от начальной точки к кабинету 443
    const pathData = `M ${startX},${startY} L ${targetX},${targetY}`;
    pathElement.setAttribute("d", pathData);

    svg.appendChild(pathElement);
}

// По умолчанию загружаем 4 этаж
changeFloor(4);
