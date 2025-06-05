// ========= Логика зума, перемещения и сенсорных жестов (без поворота) =========
let currentZoom = 1;
const zoomStep = 0.5;
const minZoom = 1;
const maxZoom = 8;

// Флаги для мыши
let isPanningMouse = false;
let startXMouse, startYMouse;

// Флаги для тач-жестов
let isPanningTouch = false;
let isGesture = false; // true, если два пальца одновременно
let initialDist = 0;
let initialZoom = 1;
let initialViewBoxX = 0;
let initialViewBoxY = 0;
let startMidX = 0;
let startMidY = 0;

// Координаты viewBox полного SVG (не масштабированного)
let viewBoxX = 0, viewBoxY = 0, viewBoxWidth, viewBoxHeight;

const svgContainer = document.getElementById("svg-container");

// Утилита для вычисления расстояния между двумя точками касания
function getDistance(touch1, touch2) {
    const dx = touch2.clientX - touch1.clientX;
    const dy = touch2.clientY - touch1.clientY;
    return Math.sqrt(dx * dx + dy * dy);
}

// Обновляем viewBox (если переданы центрированные координаты, то центрируем по ним)
function updateSvgViewBox(centerX = null, centerY = null) {
    const svgElement = svgContainer.querySelector("svg");
    if (!svgElement) return;

    // Вычисляем новые размеры полотна просмотра
    const newViewBoxWidth = viewBoxWidth / currentZoom;
    const newViewBoxHeight = viewBoxHeight / currentZoom;

    // Если заданы centerX/centerY, смещаем viewBox так, чтобы центр сохранился
    if (centerX !== null && centerY !== null) {
        viewBoxX = centerX - newViewBoxWidth / 2;
        viewBoxY = centerY - newViewBoxHeight / 2;
    }

    // Убедимся, что viewBoxX/Y не уходят в отрицательные значения
    if (viewBoxX < 0) viewBoxX = 0;
    if (viewBoxY < 0) viewBoxY = 0;

    svgElement.setAttribute('viewBox', `${viewBoxX} ${viewBoxY} ${newViewBoxWidth} ${newViewBoxHeight}`);
}

// ========= Обработка мышиных событий (панорамирование) =========
svgContainer.addEventListener('mousedown', (e) => {
    isPanningMouse = true;
    startXMouse = e.clientX;
    startYMouse = e.clientY;
    svgContainer.style.cursor = 'grabbing';
});

svgContainer.addEventListener('mousemove', (e) => {
    if (!isPanningMouse) return;
    const dx = (startXMouse - e.clientX) / currentZoom;
    const dy = (startYMouse - e.clientY) / currentZoom;
    viewBoxX += dx;
    viewBoxY += dy;
    updateSvgViewBox(); // без пересчёта центра, т.к. просто двигаем
    startXMouse = e.clientX;
    startYMouse = e.clientY;
});

svgContainer.addEventListener('mouseup', () => {
    isPanningMouse = false;
    svgContainer.style.cursor = 'grab';
});

svgContainer.addEventListener('mouseleave', () => {
    isPanningMouse = false;
    svgContainer.style.cursor = 'grab';
});

// ========= Обработка сенсорных (touch) событий для мобильных =========
svgContainer.addEventListener('touchstart', (e) => {
    e.preventDefault(); // отключаем зум страницы
    const touches = e.touches;

    if (touches.length === 1) {
        // Один палец — начинаем панорамирование
        isPanningTouch = true;
        startXMouse = touches[0].clientX;
        startYMouse = touches[0].clientY;
    } else if (touches.length === 2) {
        // Два пальца — начинаем жест (pinch-zoom)
        isGesture = true;
        isPanningTouch = false;
        // Фиксируем начальную дистанцию между пальцами
        initialDist = getDistance(touches[0], touches[1]);
        initialZoom = currentZoom;
        // Фиксируем текущее положение viewBox
        initialViewBoxX = viewBoxX;
        initialViewBoxY = viewBoxY;
        // Фиксируем начальный центр двух пальцев
        startMidX = (touches[0].clientX + touches[1].clientX) / 2;
        startMidY = (touches[0].clientY + touches[1].clientY) / 2;
    }
});

svgContainer.addEventListener('touchmove', (e) => {
    e.preventDefault();
    const touches = e.touches;
    const svgElement = svgContainer.querySelector("svg");
    if (!svgElement) return;

    if (isPanningTouch && touches.length === 1) {
        // Панорамирование одним пальцем
        const dx = (startXMouse - touches[0].clientX) / currentZoom;
        const dy = (startYMouse - touches[0].clientY) / currentZoom;
        viewBoxX += dx;
        viewBoxY += dy;
        updateSvgViewBox();
        startXMouse = touches[0].clientX;
        startYMouse = touches[0].clientY;
    } else if (isGesture && touches.length === 2) {
        // Pinch-zoom двумя пальцами

        // Считаем новую дистанцию
        const newDist = getDistance(touches[0], touches[1]);
        let scaleFactor = newDist / initialDist;
        let newZoom = initialZoom * scaleFactor;
        // Ограничиваем масштаб
        newZoom = Math.max(minZoom, Math.min(maxZoom, newZoom));

        // Сохраняем центр до изменения zoom:
        const oldViewBoxWidth = viewBoxWidth / currentZoom;
        const oldViewBoxHeight = viewBoxHeight / currentZoom;
        const centerX = viewBoxX + oldViewBoxWidth / 2;
        const centerY = viewBoxY + oldViewBoxHeight / 2;

        currentZoom = newZoom;

        // Сдвиг viewBoxX/Y так, чтобы центр оставался тем же
        updateSvgViewBox(centerX, centerY);

        // Дополнительно: можно плавно подвинуть карту в направлении движения midpoint
        // Но если требуется только центровка, то скобки ниже можно убрать:
        /*
        const currentMidX = (touches[0].clientX + touches[1].clientX) / 2;
        const currentMidY = (touches[0].clientY + touches[1].clientY) / 2;
        const midDx = (startMidX - currentMidX) / currentZoom;
        const midDy = (startMidY - currentMidY) / currentZoom;
        viewBoxX = initialViewBoxX + midDx;
        viewBoxY = initialViewBoxY + midDy;
        updateSvgViewBox(); // если раскомментировать, будет пан вместе с жестом
        */
    }
});

svgContainer.addEventListener('touchend', (e) => {
    e.preventDefault();
    if (isGesture && e.touches.length < 2) {
        isGesture = false;
        // при завершении жеста сохраняем текущее положение
        initialZoom = currentZoom;
        initialViewBoxX = viewBoxX;
        initialViewBoxY = viewBoxY;
    }
    if (isPanningTouch && e.touches.length === 0) {
        isPanningTouch = false;
    }
});

svgContainer.addEventListener('touchcancel', () => {
    isPanningTouch = false;
    isGesture = false;
});

// ========= Функции зума для кнопок (центрируем по середине экрана) =========
function zoomIn() {
    if (currentZoom < maxZoom) {
        // Сохраняем центр текущей области просмотра
        const svgElement = svgContainer.querySelector("svg");
        if (!svgElement) return;
        const oldViewBoxWidth = viewBoxWidth / currentZoom;
        const oldViewBoxHeight = viewBoxHeight / currentZoom;
        const centerX = viewBoxX + oldViewBoxWidth / 2;
        const centerY = viewBoxY + oldViewBoxHeight / 2;

        currentZoom = Math.min(maxZoom, currentZoom + zoomStep);
        updateSvgViewBox(centerX, centerY);
    }
}

function zoomOut() {
    if (currentZoom > minZoom) {
        const svgElement = svgContainer.querySelector("svg");
        if (!svgElement) return;
        const oldViewBoxWidth = viewBoxWidth / currentZoom;
        const oldViewBoxHeight = viewBoxHeight / currentZoom;
        const centerX = viewBoxX + oldViewBoxWidth / 2;
        const centerY = viewBoxY + oldViewBoxHeight / 2;

        currentZoom = Math.max(minZoom, currentZoom - zoomStep);
        updateSvgViewBox(centerX, centerY);
    }
}

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

// ========= Основная функция смены этажа =========
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

            // Инициализация viewBox из атрибутов SVG
            const viewBox = svgElement.viewBox.baseVal;
            viewBoxWidth = viewBox.width;
            viewBoxHeight = viewBox.height;
            viewBoxX = viewBox.x;
            viewBoxY = viewBox.y;
            currentZoom = 1;
            updateSvgViewBox(); // центрируем на полную область

            // Центрирование на комнате, если передали roomId
            if (roomId) {
                fetch(jsonFiles[floorNumber])
                    .then(resp => resp.json())
                    .then(jsonData => {
                        centerRoomByIndex(svgElement, jsonData, roomId);
                    })
                    .catch(err => console.error("Не удалось прочитать JSON:", err));
            }
        })
        .catch(error => console.error('Ошибка при загрузке SVG:', error));
}

// ========= Центрирование и зум на комнате по ID =========
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

function centerAndZoomOnPolygon(svgElement, shape, zoomLevel = 3) {
    const bbox = shape.getBBox();

    // Вычисляем центр фигуры
    const centerX = bbox.x + bbox.width / 2;
    const centerY = bbox.y + bbox.height / 2;

    currentZoom = zoomLevel;
    // Вычисляем новые размеры области просмотра
    const newViewBoxWidth = viewBoxWidth / currentZoom;
    const newViewBoxHeight = viewBoxHeight / currentZoom;

    viewBoxX = centerX - (newViewBoxWidth / 2);
    viewBoxY = centerY - (newViewBoxHeight / 2);

    if (viewBoxX < 0) viewBoxX = 0;
    if (viewBoxY < 0) viewBoxY = 0;

    updateSvgViewBox();
}

// ========= Поиск аудитории =========
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

// ========= Анимация временного текста =========
function showTemporaryTextWithDetails(svgElement, mainText, detailTextLine1, detailTextLine2, x, y, duration) {
    const textGroup = document.createElementNS("http://www.w3.org/2000/svg", "g");

    const triangle = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
    const width = 105;
    const height = 60;
    const points =
        `${x - width / 2},${y - height} ` +
        `${x + width / 2},${y - height} ` +
        `${x},${y}`;
    triangle.setAttribute("points", points.trim());
    triangle.setAttribute("fill", "#361778");
    triangle.setAttribute("opacity", "0.7");

    const mainTextElement = document.createElementNS("http://www.w3.org/2000/svg", "text");
    mainTextElement.setAttribute("x", x);
    mainTextElement.setAttribute("y", y - 25);
    mainTextElement.setAttribute("fill", "white");
    mainTextElement.setAttribute("font-size", "14");
    mainTextElement.setAttribute("font-weight", "bold");
    mainTextElement.setAttribute("text-anchor", "middle");
    mainTextElement.setAttribute("opacity", "0");
    mainTextElement.textContent = mainText;

    const detailTextElementLine1 = document.createElementNS("http://www.w3.org/2000/svg", "text");
    detailTextElementLine1.setAttribute("x", x);
    detailTextElementLine1.setAttribute("y", y + 15);
    detailTextElementLine1.setAttribute("fill", "white");
    detailTextElementLine1.setAttribute("font-size", "9");
    detailTextElementLine1.setAttribute("text-anchor", "middle");
    detailTextElementLine1.setAttribute("opacity", "0");
    detailTextElementLine1.textContent = detailTextLine1;

    const detailTextElementLine2 = document.createElementNS("http://www.w3.org/2000/svg", "text");
    detailTextElementLine2.setAttribute("x", x);
    detailTextElementLine2.setAttribute("y", y + 27);
    detailTextElementLine2.setAttribute("fill", "white");
    detailTextElementLine2.setAttribute("font-size", "9");
    detailTextElementLine2.setAttribute("text-anchor", "middle");
    detailTextElementLine2.setAttribute("opacity", "0");
    detailTextElementLine2.textContent = detailTextLine2;

    textGroup.appendChild(triangle);
    textGroup.appendChild(mainTextElement);
    textGroup.appendChild(detailTextElementLine1);
    textGroup.appendChild(detailTextElementLine2);
    svgElement.appendChild(textGroup);

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

// ========= Инициализация при загрузке страницы =========
(function () {
    if (typeof initialRoomId === 'undefined' || !initialRoomId) {
        changeFloor(4);
        setTimeout(() => {
            const svgElement = document.querySelector("#svg-container svg");
            if (!svgElement) return;

            const targetCircle = svgElement.getElementById("circle1");
            if (targetCircle) {
                centerAndZoomOnPolygon(svgElement, targetCircle, 4);
                showTemporaryTextWithDetails(svgElement, "Вы тут", "Деканат", "ФЦПТ", 521.45, 515.88, 2500);
            }
        }, 500);
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
