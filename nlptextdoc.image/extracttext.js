function extractText(debug = false) {
    try {
        console.log("Starting DOM traversal ...");
        document.body.focus();
        window.drawRectangles = debug;
        window.altLetter = 0;
        window.pageElements = new Map();
        var htmlNode = document.children[0];
        window.pageRoot = createPageElement(htmlNode);
        window.pageElements.set(htmlNode, window.pageRoot);
        visitBlock(document.body);
        prunePageElements(window.pageRoot.children);
        console.log("OK, result ready");
        return JSON.stringify(window.pageRoot);
    } catch (error) {
        if (typeof (error.stack) === "undefined") {
            return "ERROR:" + error;
        } else {
            return "ERROR:" + error.stack;
        }
    }
}

class PageElement {
    constructor(tagName, classNames, boundingBox) {
        this.tagName = tagName;
        this.classNames = classNames;
        this.boundingBox = boundingBox;
        this.children = null;
    }

    appendChild(child) {
        if (this.children == null) {
            this.children = [];
        }
        this.children.push(child);
    }

    hasText() {
        return "text" in this;
    }

    hasOneChild() {
        return this.children != null && this.children.length == 1;
    }
}

class TextBlock extends PageElement {
    constructor(tagName, classNames, text, words) {
        var boundingBox = getBoundingBoxAround(words);
        super(tagName, classNames, boundingBox);
        this.text = text;
        this.lines = createLinesFromWords(words);
    }
}

class TextLine {
    constructor(firstWord) {
        this.text = firstWord.text;
        this.boundingBox = firstWord.boundingBox;
        this.words = [];
        this.words.push(firstWord);
    }

    appendWord(nextWord) {
        this.words.push(nextWord);
        this.boundingBox = getBoundingBoxAround([this, nextWord]);
        this.text = this.text + " " + nextWord.text;
    }

    end() { }
}

class Word {
    constructor(text, letters) {
        this.text = text;
        this.boundingBox = getBoundingBoxAround(letters);
        this.letters = letters;
    }
}

class Letter {
    constructor(char, boundingBox) {
        this.char = char;
        this.boundingBox = boundingBox;
    }
}

class TextLabel extends PageElement {
    constructor(tagName, classNames, boundingBox, text) {
        super(tagName, classNames, boundingBox);
        this.text = text;
    }
}

class BoundingBox {
    constructor(x, y, width, height) {
        this.x = Math.round(x);
        this.y = Math.round(y);
        this.width = Math.round(width);
        this.height = Math.round(height);
        if (!isFinite(x) || !isFinite(y) || !isFinite(width) || !isFinite(height)) {
            throw new Error("Bounding box coordinates are not finite");
        }
    }
}

function getBoundingBoxAround(objs) {
    var left = Number.MAX_VALUE;
    var right = Number.MIN_VALUE;
    var top = Number.MAX_VALUE;
    var bottom = Number.MIN_VALUE;
    for (var i = 0; i < objs.length; i++) {
        var box = objs[i].boundingBox;
        if (left > box.x) left = box.x;
        if (top > box.y) top = box.y;
        if ((box.x + box.width) > right) right = box.x + box.width;
        if ((box.y + box.height) > bottom) bottom = box.y + box.height;
    }
    return new BoundingBox(left, top, right - left, bottom - top);
}

function createPageElement(domElt, type = null, text = null, words = null) {
    var pageElt = null;
    if (type == "TextBlock") {
        pageElt = new TextBlock(domElt.tagName, domElt.className, text, words);
        if (window.drawRectangles) {
            for (var lineIdx = 0; lineIdx < pageElt.lines.length; lineIdx++) {
                var line = pageElt.lines[lineIdx];
                for (var wordIdx = 0; wordIdx < line.words.length; wordIdx++) {
                    var word = line.words[wordIdx];
                    for (var letterIdx = 0; letterIdx < word.letters.length; letterIdx++) {
                        var letter = word.letters[letterIdx];
                        drawRectangle(letter.boundingBox, window.altLetter % 2 == 0 ? "#AAAAFF" : "#5555FF", 0.4, "none", 1, "black");
                        window.altLetter++;
                    }
                    drawRectangle(word.boundingBox, "white", 0, "solid", 1, "blue");
                }
                drawRectangle(line.boundingBox, "white", 0, "solid", 1, "green");
            }
            drawRectangle(pageElt.boundingBox, "white", 0, "solid", 1, "red");
        }
    } else if (type == "TextLabel") {
        pageElt = new TextLabel(domElt.tagName, domElt.className, getBoundingBox(domElt), text);
        if (window.drawRectangles) {
            drawRectangle(pageElt.boundingBox, "yellow", 0.4, "solid", 1, "orange");
        }
    } else {
        pageElt = new PageElement(domElt.tagName, domElt.className, getBoundingBox(domElt));
    }
    window.pageElements.set(domElt, pageElt);
    // Recursively create parents chain
    var domParent = domElt.parentElement;
    if (domParent != null) {
        var pageParent = null;
        if (window.pageElements.has(domParent)) {
            pageParent = window.pageElements.get(domParent);
        } else {
            pageParent = createPageElement(domParent);
        }
        pageParent.appendChild(pageElt);
    }
    return pageElt;
}

function prunePageElements(pageEltsList) {
    for (var i = 0; i < pageEltsList.length; i++) {
        var pageElt = pageEltsList[i];
        var replacePageElt = pageElt;
        while (!replacePageElt.hasText() && replacePageElt.hasOneChild()) {
            replacePageElt = replacePageElt.children[0];
        }
        if (replacePageElt != pageElt) {
            pageEltsList[i] = replacePageElt;
        }
        if (replacePageElt.children != null) {
            prunePageElements(replacePageElt.children);
        }
    }
}

function getBoundingBox(elt) {
    var rect = elt.getBoundingClientRect();
    return new BoundingBox(rect.left, rect.top, rect.width, rect.height);
}

function createLinesFromWords(words) {
    var lines = [];
    var lastLine = null;
    for (var i = 0; i < words.length; i++) {
        var word = words[i];
        if (lastLine == null) {
            lastLine = new TextLine(word);
            lines.push(lastLine);
        } else {
            if (word.boundingBox.y > (lastLine.boundingBox.y + lastLine.boundingBox.height / 2)) {
                lastLine.end();
                lastLine = new TextLine(word);
                lines.push(lastLine);
            } else {
                lastLine.appendWord(word);
            }
        }
    }
    lastLine.end();
    return lines;
}

String.prototype.capitalize = function () {
    return this.replace(/(?:^|\s)\S/g, function (a) { return a.toUpperCase(); });
};

function visitBlock(node, textBoundingRect = null) {
    if (isVisible(node)) {
        if (node.tagName == "INPUT" && node.type == "text") {
            if (node.placeholder != "") {
                createPageElement(node, "TextLabel", node.placeholder);
            } else if (node.value != "" && node.value.trim() != "") {
                createPageElement(node, "TextLabel", node.value.trim());
            }
        } else if (node.tagName == "INPUT" && (node.type == "submit" || node.type == "button") && node.value != "") {
            createPageElement(node, "TextLabel", node.value);
        } else if (node.tagName == "IFRAME") {
            // If the iframe points to a url in a different domain,
            // any attempt to check for node.contentDocument
            // will throw a security error which is impossible to catch
            // => we can only ignore iframe contents ...
        } else if (node.hasChildNodes()) {
            var inlineContext = [];
            inlineContext.textBoundingRect = textBoundingRect;
            var childNodes = getChildNodesWithPseudoElements(node);
            for (var i = 0; i < childNodes.length; i++) {
                child = childNodes[i];
                visitNode(node, child, inlineContext);
            }
            writeAndResetInlineContext(node, inlineContext);
        }
    } else {
        for (var i = 0; i < node.childNodes.length; i++) {
            child = node.childNodes[i];
            if (child.nodeType == Node.ELEMENT_NODE) {
                visitBlock(child);
            }
        }
    }
}

function visitNode(node, child, inlineContext) {
    if (child.nodeType == Node.TEXT_NODE) {
        pushTextNodeToInlineContext(node, child, inlineContext);
    } else if (child.nodeType == Node.ELEMENT_NODE) {
        if (child.tagName != "SCRIPT") {
            var childStyle = window.getComputedStyle(child, null);
            var displayStyle = childStyle.getPropertyValue('display');
            if (displayStyle && displayStyle == "inline") {
                visitInline(child, inlineContext);
            } else {
                inlineContext = writeAndResetInlineContext(node, inlineContext);
                var overflowStyle = childStyle.getPropertyValue('overflow');
                var textBoundingRect = null;
                if (overflowStyle == "hidden" || overflowStyle == "scroll") {
                    textBoundingRect = child.getBoundingClientRect();
                }
                visitBlock(child, textBoundingRect);
            }
        }
    }
}

function visitInline(node, inlineContext) {
    if (isVisible(node) && node.hasChildNodes()) {
        var childNodes = getChildNodesWithPseudoElements(node);
        for (var i = 0; i < childNodes.length; i++) {
            child = childNodes[i];
            visitNode(node, child, inlineContext);
        }
    }
}

function pushTextNodeToInlineContext(node, child, inlineContext) {
    if (child.nodeValue.length > 0) {
        var nodeStyle = window.getComputedStyle(node, null);
        var textTransformStyle = nodeStyle.getPropertyValue('text-transform');
        if (textTransformStyle == "uppercase") {
            child.nodeValue = child.nodeValue.toUpperCase();
        } else if (textTransformStyle == "lowercase") {
            child.nodeValue = child.nodeValue.toLowerCase();
        } else if (textTransformStyle == "capitalize") {
            child.nodeValue = child.nodeValue.capitalize();
        }
        inlineContext.push(child);
    }
}

function writeAndResetInlineContext(node, inlineContext) {
    if (inlineContext.length == 0) {
        return inlineContext;
    } else {
        var lastCharWasSpace = true;
        var text = "";
        var words = [];
        for (var i = 0; i < inlineContext.length; i++) {
            var textNode = inlineContext[i];
            var textNodeValue = textNode.nodeValue;
            var wordStartIndex = 0;
            for (var j = 0; j < textNodeValue.length; j++) {
                var char = textNodeValue.charAt(j);
                if (char == " " || char == "\xa0" || char == "\t" || char == "\n" || char == "\r") {
                    if (lastCharWasSpace) {
                        // ignore repeated spaces
                        wordStartIndex++;
                    } else {
                        var word = createWord(textNode, wordStartIndex, j);
                        if (word != null) {
                            addWordIfVisible(words, inlineContext.textBoundingRect, word);
                        }
                        text += " ";
                        lastCharWasSpace = true;
                        wordStartIndex = j + 1;
                    }
                } else {
                    text += char;
                    lastCharWasSpace = false;
                }
            }
            if (wordStartIndex < textNodeValue.length) {
                var word = createWord(textNode, wordStartIndex, textNodeValue.length);
                if (word != null) {
                    addWordIfVisible(words, inlineContext.textBoundingRect, word);
                }
            }
        }
        if (words.length > 0) {
            var textBlock = createPageElement(node, "TextBlock", text, words);
        }
        inlineContext.length = 0;
        return inlineContext;
    }
}

function addWordIfVisible(words, textBoundingRect, word) {
    var addWord = true;
    if (textBoundingRect != null) {
        addWord = addWord && (word.boundingBox.x >= Math.floor(textBoundingRect.left));
        addWord = addWord && ((word.boundingBox.x + word.boundingBox.width) <= Math.floor(textBoundingRect.right + 1));
        addWord = addWord && (word.boundingBox.y >= Math.floor(textBoundingRect.top));
        addWord = addWord && ((word.boundingBox.y + word.boundingBox.height) <= Math.floor(textBoundingRect.bottom + 1));
    }
    if (addWord) {
        words.push(word);
    }
}

function createWord(textNode, wordStartIndex, wordEndIndex) {
    var word = null;
    var wordTxt = textNode.nodeValue.substring(wordStartIndex, wordEndIndex);
    if (wordTxt.trim().length > 0) {
        // Create letters
        var letters = []
        var range = document.createRange();
        if (textNode.parentNode != null) {
            for (var i = wordStartIndex; i < wordEndIndex; i++) {
                range.setStart(textNode, i);
                range.setEnd(textNode, i + 1);
                var box = getBoundingBox(range);
                var letter = new Letter(textNode.nodeValue.charAt(i), box);
                if (letters.length > 0) {
                    var lastLetter = letters[letters.length - 1];
                    if ((lastLetter.boundingBox.x + lastLetter.boundingBox.width - 1) > letter.boundingBox.x) {
                        lastLetter.boundingBox.width = letter.boundingBox.x - lastLetter.boundingBox.x + 1;
                    }
                }
                letters.push(letter);
            }
            // Special case for pseudo-elements
        } else if (textNode.parent != null) {
            var parentRect = textNode.parent.getBoundingClientRect();
            var pseudoEltStyle = textNode.style;
            var wordBox = null;
            if (pseudoEltStyle.position == "absolute" && isFinite(intFromPixels(pseudoEltStyle.left)) && 
                isFinite(intFromPixels(pseudoEltStyle.width)) && isFinite(intFromPixels(pseudoEltStyle.height))) {
                if (textNode.sibling != null) {
                    if (textNode.before) {
                        range.setStart(textNode.sibling, 0);
                        range.setEnd(textNode.sibling, 1);
                        var followingLetterBox = getBoundingBox(range);
                        wordBox = new BoundingBox(parentRect.left + intFromPixels(pseudoEltStyle.left), followingLetterBox.y + (followingLetterBox.height - intFromPixels(pseudoEltStyle.height)) / 2, intFromPixels(pseudoEltStyle.width), intFromPixels(pseudoEltStyle.height));
                    } else {
                        var siblingLength = null;
                        // Element
                        if (textNode.sibling.nodeType == 1) {
                            siblingLength = textNode.childNodes.length;
                            // Text
                        } else if (textNode.sibling.nodeType == 3) {
                            siblingLength = textNode.sibling.nodeValue.length;
                        }
                        range.setStart(textNode.sibling, siblingLength - 1);
                        range.setEnd(textNode.sibling, siblingLength);
                        var precedingLetterBox = getBoundingBox(range);
                        wordBox = new BoundingBox(parentRect.left + intFromPixels(pseudoEltStyle.left), precedingLetterBox.y + (precedingLetterBox.height - intFromPixels(pseudoEltStyle.height)) / 2, intFromPixels(pseudoEltStyle.width), intFromPixels(pseudoEltStyle.height));
                    }
                } else {
                    wordBox = new BoundingBox(parentRect.left + intFromPixels(pseudoEltStyle.left), parentRect.top + intFromPixels(pseudoEltStyle.top), intFromPixels(pseudoEltStyle.width), intFromPixels(pseudoEltStyle.height));
                }
            } else {
                // Old code - needs to be fixed
                range.setStart(textNode.parent, 0);
                range.setEnd(textNode.parent, 1);
                var textRect = range.getBoundingClientRect();
                var pseudoElementWidth = parentRect.width - textRect.width;
                if (parentRect.left == textRect.left) {
                    wordBox = new BoundingBox(parentRect.left + parentRect.width - pseudoElementWidth, parentRect.top, pseudoElementWidth, parentRect.height);
                } else {
                    wordBox = new BoundingBox(parentRect.left, parentRect.top, pseudoElementWidth, parentRect.height);
                }
            }
            if (wordBox != null && isFinite(wordBox.x) && isFinite(wordBox.y) && isFinite(wordBox.width) && isFinite(wordBox.height)) {
                var content = textNode.nodeValue;
                var letterWidth = wordBox.width / content.length;
                for (var i = wordStartIndex; i < wordEndIndex; i++) {
                    var letterBox = new BoundingBox(wordBox.x + i * letterWidth, wordBox.y, letterWidth, wordBox.height);
                    var letter = new Letter(textNode.nodeValue.charAt(i), letterBox);
                    letters.push(letter);
                }
            }
        }
        word = new Word(wordTxt, letters);
    }
    return word;
}

function intFromPixels(pxval) {
    return parseInt(pxval.substring(0, pxval.length - 2));
}

function getChildNodesWithPseudoElements(node) {
    var childNodesWithPseudoElements = [];
    var beforeStyle = window.getComputedStyle(node, "::before")
    var beforeTxt = beforeStyle.getPropertyValue("content");
    if (beforeTxt != "none" && isNotIcon(beforeTxt)) {
        if (!((beforeStyle.display === 'none') || (beforeStyle.visibility !== 'visible'))) {
            var beforeNode = document.createTextNode(beforeTxt.substring(1, beforeTxt.length - 1));
            beforeNode.parent = node;
            beforeNode.sibling = node.hasChildNodes() ? node.childNodes[0] : null;
            beforeNode.before = true;
            beforeNode.style = beforeStyle;
            childNodesWithPseudoElements.push(beforeNode);
            var spaceNode = document.createTextNode(" ");
            childNodesWithPseudoElements.push(spaceNode);
        }
    }
    for (var i = 0; i < node.childNodes.length; i++) {
        child = node.childNodes[i];
        childNodesWithPseudoElements.push(child);
    }
    var afterStyle = window.getComputedStyle(node, "::after");
    var afterTxt = afterStyle.getPropertyValue("content");
    if (afterTxt != "none" && isNotIcon(afterTxt)) {
        if (!((afterStyle.display === 'none') || (afterStyle.visibility !== 'visible'))) {
            var spaceNode = document.createTextNode(" ");
            childNodesWithPseudoElements.push(spaceNode);
            var afterNode = document.createTextNode(afterTxt.substring(1, afterTxt.length - 1));
            afterNode.parent = node;
            afterNode.sibling = node.hasChildNodes() ? node.childNodes[node.childNodes.length - 1] : null;
            afterNode.before = false;
            afterNode.style = afterStyle;
            childNodesWithPseudoElements.push(afterNode);
        }
    }
    return childNodesWithPseudoElements;
}

function isNotIcon(text) {
    for (var i = 0; i < text.length; i++) {
        var charCode = text.charCodeAt(i);
        // Filter on valid ISO8859-1 codes
        if (!((charCode >= 32 && charCode <= 127) || (charCode >= 160 && charCode <= 255))) {
            return false;
        }
    }
    return true;
}

function isVisible(elem) {
    try {

        if (!(elem instanceof Element)) return false;
        const style = getComputedStyle(elem);
        if (style.display === 'none') return false;
        if (style.visibility !== 'visible') return false;
        if (style.opacity < 0.1) return false;
        if (elem.offsetWidth + elem.offsetHeight + elem.getBoundingClientRect().height +
            elem.getBoundingClientRect().width === 0) {
            return false;
        }
        const elemCenter = {
            x: elem.getBoundingClientRect().left + elem.offsetWidth / 2,
            y: elem.getBoundingClientRect().top + elem.offsetHeight / 2
        };
        if (!isFinite(elemCenter.x) || elemCenter.x < 0) return false;
        if (elemCenter.x > (document.documentElement.clientWidth || window.innerWidth)) return false;
        if (!isFinite(elemCenter.y) || elemCenter.y < 0) return false;
        if (elemCenter.y > (document.documentElement.clientHeight || window.innerHeight)) return false;
        // Handle all kinds of layouts for text lines
        for (var containerXPart = 0; containerXPart < 3; containerXPart++) {
            for (var containerYPart = 0; containerYPart < 6; containerYPart++) {
                var pointContainer = document.elementFromPoint(elemCenter.x + (containerXPart / 2 - (containerXPart % 2)) * 4 * elem.offsetWidth / 10, elemCenter.y - containerYPart * elem.offsetHeight / 12);
                if (pointContainer != null) {
                    do {
                        if (pointContainer === elem) return true;
                    } while (pointContainer = pointContainer.parentNode);
                }
            }
        }
        return false;

    } catch (err) {
        return false;
    }
}

function drawRectangle(box, color, opacity, borderStyle, borderWidth, borderColor) {
    var overlay = document.createElement("div");
    overlay.style.position = "fixed";
    overlay.style.left = box.x + "px";
    overlay.style.top = box.y + "px";
    overlay.style.width = box.width + "px";
    overlay.style.height = box.height + "px";
    if (opacity > 0) {
        overlay.style.backgroundColor = color;
        overlay.style.opacity = opacity;
    }
    overlay.style.borderStyle = borderStyle;
    overlay.style.borderWidth = borderWidth + "px";
    overlay.style.borderColor = borderColor;
    overlay.style.zIndex = 100000;
    document.body.append(overlay);
    return overlay;
}