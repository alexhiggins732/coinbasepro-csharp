
var rows = getData();
//console.log(rows);
    function unpack(rows, key) {
        return rows.map(function (row) { return parseFloat(row[key]); });
    }

    var data = [
        {
            type: "surface",
            x: [0, 0, 0, 0, 1, 1, 1, 1],
            y: [0, 1, 0, 1, 0, 1, 0, 1],
            z: [1, 1, 0, 0, 1, 1, 0, 0],
            //value: [1, 2, 3, 4, 5, 6, 7, 8],
            //isomin: 2,
            //isomax: 4,
            //isomin: -10,
            //isomax: 10,
            //surface: { show: true, count: 4, fill: 1, pattern: 'odd' },
            //caps: {
            //    x: { show: true },
            //    y: { show: true },
            //    z: { show: true }
            //},
        }
    ];

    var layout = {
        margin: { t: 0, l: 0, b: 0 },
        scene: {
            camera: {
                eye: {
                    x: 1.86,
                    y: 0.61,
                    z: 0.98
                }
            }
        }
    };

    Plotly.newPlot('myDiv', data, layout, { showSendToCloud: true });

function getData() {
    var text = document.getElementById("rawData").innerText;
    var lines = text.split('\n');
    var rows = [];
    for (var i = 0; i < lines.length; i++) {
        var ln = lines[i];
        //console.log(ln);
        var parts = ln.split(',');
        //console.log(parts);
        var p1 = parts[1];
        //console.log(p1);
        var f1 = parseFloat(p1);
        //console.log(f1);
        var rw = getRow(parseInt(parts[0]), parseFloat(parts[1]), parseFloat(parts[2]), parseFloat(parts[3]), parseFloat(parts[4]));
        rows.push(rw);
    }
    return rows;
}
var getRowIndex = 0;
function getRow(i, x, y, z, value) {
    return { "":i, x: x, y: y, z: z, value: value };
}

