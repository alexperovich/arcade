from typing import List
from enum import Enum


class TestResult:
    def __init__(self, name, kind, type_name, method, duration, status, exception_type, failure_message, stack_trace,
                 skip_reason, attachments, attempts=None):
        """

        :type name: unicode
        :type kind: unicode
        :type type_name: unicode
        :type method: unicode
        :type duration: float
        :type status: TestStatus
        :type exception_type: unicode
        :type failure_message: unicode
        :type stack_trace: unicode
        :type skip_reason: unicode
        :type attachments: List[TestResultAttachment]
        :type attempts: List[TestResult]
        """
        self._name = name
        self._kind = kind
        self._type = type_name
        self._method = method
        self._duration_seconds = duration
        self._status = status
        self._exception_type = exception_type
        self._failure_message = failure_message
        self._stack_trace = stack_trace
        self._skip_reason = skip_reason
        self._attachments = attachments
        self._attempts = attempts

    @property
    def name(self):
        return self._name

    @property
    def kind(self):
        return self._kind

    @property
    def type(self):
        return self._type

    @property
    def method(self):
        return self._method

    @property
    def duration_seconds(self):
        return self._duration_seconds

    @property
    def status(self):
        return self._status

    @property
    def exception_type(self):
        return self._exception_type

    @property
    def failure_message(self):
        return self._failure_message

    @property
    def stack_trace(self):
        return self._stack_trace

    @property
    def skip_reason(self):
        return self._skip_reason

    @property
    def attachments(self):
        return self._attachments

    @property
    def attempts(self):
        return self._attempts


class TestStatus(Enum):
    none = 1
    passed = 2
    failed = 3
    skipped = 4


class TestResultAttachment:
    def __init__(self, name, text):
        """

        :type name: unicode
        :type text: unicode
        """
        self._name = name
        self._text = text

    @property
    def name(self):
        return self._name

    @property
    def text(self):
        return self._text
