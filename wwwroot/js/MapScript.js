// ========= Логика зума и перемещения =========
let currentZoom = 1;
const zoomStep = 0.3;
const minZoom = 1;
const maxZoom = 4;
let isPanning = false, startX, startY;
let viewBoxX = 0, viewBoxY = 0, viewBoxWidth, viewBoxHeight;

const svgContainer = document.getElementById("svg-container");

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
    const svgElement = svgContainer.querySelector("svg");
    if (!svgElement) return;

    const newViewBoxWidth = viewBoxWidth / currentZoom;
    const newViewBoxHeight = viewBoxHeight / currentZoom;
    svgElement.setAttribute('viewBox', `${viewBoxX} ${viewBoxY} ${newViewBoxWidth} ${newViewBoxHeight}`);
}

// Обработка перемещения
svgContainer.addEventListener('mousedown', (e) => {
    isPanning = true;
    startX = e.clientX;
    startY = e.clientY;
    svgContainer.style.cursor = 'grabbing';
});

svgContainer.addEventListener('mousemove', (e) => {
    if (!isPanning) return;
    const dx = (startX - e.clientX) / currentZoom;
    const dy = (startY - e.clientY) / currentZoom;
    viewBoxX += dx;
    viewBoxY += dy;
    updateSvgViewBox();
    startX = e.clientX;
    startY = e.clientY;
});

svgContainer.addEventListener('mouseup', () => {
    isPanning = false;
    svgContainer.style.cursor = 'grab';
});

svgContainer.addEventListener('mouseleave', () => {
    isPanning = false;
    svgContainer.style.cursor = 'grab';
});

// ========= Пути к SVG/JSON файлов этажей =========
const svgFiles = {
    1: '/img/FloorPlans/1fl.svg',
    2: '/img/FloorPlans/2fl1.svg',
    3: '/img/FloorPlans/3fl4.svg',
    4: '/img/FloorPlans/4fl2.svg',
    5: '/img/FloorPlans/5fl6.svg'
};

const jsonFiles = {
    1: '/img/FloorPlans/1fl.json',
    2: '/img/FloorPlans/2fl.json',
    3: '/img/FloorPlans/3fl.json',
    4: '/img/FloorPlans/4fl.json',
    5: '/img/FloorPlans/5fl.json'
};

// Основная функция смены этажа
function changeFloor(floorNumber, roomId = "") {
    document.getElementById("floor-title").innerText = floorNumber + " этаж";

    fetch(svgFiles[floorNumber])
        .then(response => response.text())
        .then(svgContent => {
            svgContainer.innerHTML = svgContent;
            const svgElement = svgContainer.querySelector("svg");
            if (!svgElement) {
                console.error("SVG не найден:", svgFiles[floorNumber]);
                return;
            }

            // Инициализация viewBox
            const viewBox = svgElement.viewBox.baseVal;
            viewBoxWidth = viewBox.width;
            viewBoxHeight = viewBox.height;
            viewBoxX = viewBox.x;
            viewBoxY = viewBox.y;
            currentZoom = 1;
            updateSvgViewBox();

            // При roomId выполняем центрирование
            if (roomId) {
                fetch(jsonFiles[floorNumber])
                    .then(resp => resp.json())
                    .then(jsonData => {
                        centerRoomByIndex(svgElement, jsonData, roomId);
                    })
                    .catch(err => console.error(`Не удалось прочитать JSON:`, err));
            }
        })
        .catch(error => console.error('Ошибка при загрузке SVG:', error));
}

// Центрирование и зум на комнате по ID
function centerRoomByIndex(svgElement, jsonData, roomId) {
    const idx = jsonData.rooms.findIndex(r => r.room === roomId);
    if (idx === -1) {
        console.warn(`Комната room=${roomId} не найдена.`);
        return;
    }

    const targetId = jsonData.rooms[idx].id;
    const targetShape = svgElement.getElementById(targetId);

    if (!targetShape) {
        console.warn(`Shape с ID ${targetId} не найден.`);
        return;
    }

    centerAndZoomOnPolygon(svgElement, targetShape, 4);
}

// Центрирование и зум на фигуре
function centerAndZoomOnPolygon(svgElement, shape, zoomLevel = 3) {
    const bbox = shape.getBBox();

    currentZoom = zoomLevel;

    const newViewBoxWidth = viewBoxWidth / currentZoom;
    const newViewBoxHeight = viewBoxHeight / currentZoom;

    const centerX = bbox.x + bbox.width / 2;
    const centerY = bbox.y + bbox.height / 2;

    viewBoxX = centerX - (newViewBoxWidth / 2);
    viewBoxY = centerY - (newViewBoxHeight / 2);

    if (viewBoxX < 0) viewBoxX = 0;
    if (viewBoxY < 0) viewBoxY = 0;

    updateSvgViewBox();
}

// Функция поиска аудитории
function searchRoom() {
    const roomIdInput = document.getElementById('searchInput').value.trim();
    if (!roomIdInput) return;

    const firstChar = roomIdInput.charAt(0);
    const floorCandidate = parseInt(firstChar, 10);

    if (!isNaN(floorCandidate) && floorCandidate >= 1 && floorCandidate <= 5) {
        changeFloor(floorCandidate, roomIdInput);
    } else {
        tryFindRoomInAllFloors(roomIdInput);
    }
}

// Перебор этажей для поиска аудитории
function tryFindRoomInAllFloors(roomId) {
    const floorNumbers = [1, 2, 3, 4, 5];

    (function checkNextFloor(i) {
        if (i >= floorNumbers.length) {
            console.warn("Комната", roomId, "не найдена.");
            return;
        }

        const fNum = floorNumbers[i];
        fetch(jsonFiles[fNum])
            .then(resp => resp.json())
            .then(jsonData => {
                const idx = jsonData.rooms.findIndex(r => r.room === roomId);
                if (idx !== -1) {
                    changeFloor(fNum, roomId);
                } else {
                    checkNextFloor(i + 1);
                }
            })
            .catch(err => {
                console.error("Ошибка чтения JSON:", err);
                checkNextFloor(i + 1);
            });
    })(0);
}

// Добавляем функцию для отображения текста с анимацией и треугольным фоном
function showTemporaryTextWithDetails(svgElement, mainText, detailTextLine1, detailTextLine2, x, y, duration) {
    // Группа для текста и фона
    const textGroup = document.createElementNS("http://www.w3.org/2000/svg", "g");

    // Создаём треугольник для текста "Вы тут"
    const triangle = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
    const width = 105; // Уменьшенная ширина треугольника
    const height = 60; // Высота треугольника

    // Координаты вершин треугольника
    const points = `
        ${x - width / 2},${y - height} 
        ${x + width / 2},${y - height} 
        ${x},${y}
    `;
    triangle.setAttribute("points", points.trim());
    triangle.setAttribute("fill", "#361778");
    triangle.setAttribute("opacity", "0.7"); // Полупрозрачная заливка

    // Создаём основной текст "Вы тут"
    const mainTextElement = document.createElementNS("http://www.w3.org/2000/svg", "text");
    mainTextElement.setAttribute("x", x);
    mainTextElement.setAttribute("y", y - 25); // Смещение вверх
    mainTextElement.setAttribute("fill", "white");
    mainTextElement.setAttribute("font-size", "14");
    mainTextElement.setAttribute("font-weight", "bold");
    mainTextElement.setAttribute("text-anchor", "middle");
    mainTextElement.setAttribute("opacity", "0");
    mainTextElement.textContent = mainText;

    // Создаём первую строку текста "Деканат"
    const detailTextElementLine1 = document.createElementNS("http://www.w3.org/2000/svg", "text");
    detailTextElementLine1.setAttribute("x", x);
    detailTextElementLine1.setAttribute("y", y + 15); // Смещение вниз
    detailTextElementLine1.setAttribute("fill", "white");
    detailTextElementLine1.setAttribute("font-size", "9");
    detailTextElementLine1.setAttribute("text-anchor", "middle");
    detailTextElementLine1.setAttribute("opacity", "0");
    detailTextElementLine1.textContent = detailTextLine1;

    // Создаём вторую строку текста "ФЦПТ"
    const detailTextElementLine2 = document.createElementNS("http://www.w3.org/2000/svg", "text");
    detailTextElementLine2.setAttribute("x", x);
    detailTextElementLine2.setAttribute("y", y + 27); // Ещё ниже
    detailTextElementLine2.setAttribute("fill", "white");
    detailTextElementLine2.setAttribute("font-size", "9");
    detailTextElementLine2.setAttribute("text-anchor", "middle");
    detailTextElementLine2.setAttribute("opacity", "0");
    detailTextElementLine2.textContent = detailTextLine2;

    // Добавляем элементы в группу
    textGroup.appendChild(triangle);
    textGroup.appendChild(mainTextElement);
    textGroup.appendChild(detailTextElementLine1);
    textGroup.appendChild(detailTextElementLine2);
    svgElement.appendChild(textGroup);

    // Анимация появления
    mainTextElement.animate([{ opacity: 0 }, { opacity: 1 }], {
        duration: 500,
        fill: "forwards",
    });
    triangle.animate([{ opacity: 0 }, { opacity: 0.7 }], {
        duration: 500,
        fill: "forwards",
    });
    [detailTextElementLine1, detailTextElementLine2].forEach((el) => {
        el.animate([{ opacity: 0 }, { opacity: 1 }], {
            duration: 500,
            fill: "forwards",
        });
    });

    // Анимация исчезновения
    setTimeout(() => {
        mainTextElement.animate([{ opacity: 1 }, { opacity: 0 }], {
            duration: 500,
            fill: "forwards",
        });
        triangle.animate([{ opacity: 0.7 }, { opacity: 0 }], {
            duration: 500,
            fill: "forwards",
        });
        [detailTextElementLine1, detailTextElementLine2].forEach((el) => {
            el.animate([{ opacity: 1 }, { opacity: 0 }], {
                duration: 500,
                fill: "forwards",
            });
        }).onfinish = () => {
            svgElement.removeChild(textGroup);
        };
    }, duration - 500);
}

// Модифицируем инициализацию, чтобы при загрузке 4-го этажа центрироваться на circle1
(function () {
    if (typeof initialRoomId === 'undefined' || !initialRoomId) {
        changeFloor(4);
        setTimeout(() => {
            const svgElement = document.querySelector("#svg-container svg");
            if (!svgElement) return;

            const targetCircle = svgElement.getElementById("circle1");
            if (targetCircle) {
                centerAndZoomOnPolygon(svgElement, targetCircle, 4);
                showTemporaryTextWithDetails(svgElement, "Вы тут", "Деканат", "ФЦПТ", 521.45, 515.88, 2500); // 2.5 секунды
            }
        }, 500); // Даем время для загрузки SVG
        return;
    }

    const firstChar = initialRoomId.charAt(0);
    const floorCandidate = parseInt(firstChar, 10);

    if (!isNaN(floorCandidate) && floorCandidate >= 1 && floorCandidate <= 5) {
        changeFloor(floorCandidate, initialRoomId);
    } else {
        tryFindRoomInAllFloors(initialRoomId);
    }
})();
