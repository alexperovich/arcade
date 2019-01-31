import os
import sys
import traceback
from Queue import Queue
from threading import Thread

from helix.saferequests import request_with_retry
import requests

from test_results_reader import read_results
from helpers import batch
from azure_devops_result_publisher import AzureDevOpsTestResultPublisher
from defs import TestResult, TestStatus
from typing import List


class UploadWorker(Thread):
    def __init__(self, queue, idx, collection_uri, team_project, test_run_id, access_token):
        super(UploadWorker, self).__init__()
        self.queue = queue
        self.idx = idx
        self.publisher = AzureDevOpsTestResultPublisher(
            collection_uri=collection_uri,
            access_token=access_token,
            team_project=team_project,
            test_run_id=test_run_id,
        )
        self.total_uploaded = 0
  
    def __print(self, msg):
        sys.stdout.write('Worker {}: {}\n'.format(self.idx, msg))
        sys.stdout.flush()

    def __process(self, batch):
        self.publisher.upload_batch(batch)
        self.total_uploaded = self.total_uploaded + len(batch)
        self.__print('uploaded {} results'.format(self.total_uploaded))

    def run(self):
        self.__print("starting...")
        while True:
            item = self.queue.get()
            try:
                self.__process(item)
            except:
                self.__print("got error: {}".format(traceback.format_exc()))
            finally:
                self.queue.task_done()


def process_args():
    if len(sys.argv) < 6 or len(sys.argv) > 6:
        sys.exit("Expected 5 arguments")

    collection_uri = sys.argv[1]
    team_project = sys.argv[2]
    test_run_id = sys.argv[3]
    access_token = sys.argv[4]
    max_retries = int(sys.argv[5])

    return collection_uri, team_project, test_run_id, access_token, max_retries


def get_previous_attempt_info():
    return ["https://test/testResults.xml", "https://test2/test_results.xml"]

def get_test_results():
    file, results = read_results(os.getcwd())
    return file, results

def have_failures(test_results):
    """
    : type test_result: List[TestResult]
    """

    for result in test_results:
        if result.status == TestStatus.failed:
            return False
        
    return True
    

def download_file(self, uri, destination):
    if os.path.exists(destination):
        os.remove(destination)

    response = request_with_retry(
        lambda: requests.get(uri, stream=True, timeout=15.0)
    )

    print("response received. Status: {} Elapsed time: {}".format(response.status_code, response.elapsed))

    if response.status_code == 200:
        with open(destination, 'wb') as downloaded_file:
            for chunk in response.iter_content(chunk_size=1024):
                if chunk:
                    downloaded_file.write(chunk)
            downloaded_file.flush()
    else:
        raise Exception("Failed to download '{}', the status code was {}.".format(str(uri).split("?")[0], response.status_code))


class TestResultUploader():
    def __init__(self, collection_uri, team_project, test_run_id, access_token, max_retries):
        self.collection_uri = collection_uri
        self.team_project = team_project
        self.test_run_id = test_run_id
        self.access_token = access_token
        self.max_retries = max_retries


    def run(self):
        current_results_file, current_results = get_test_results()

        if self.max_retries <= 1:
            self.process_results(current_results)
            return
        
        previous_attempts = get_previous_attempt_info()
        current_attempt = len(previous_attempts) + 1

        if have_failures(current_results) and current_attempt < self.max_retries:
            self.enqueue_retry(current_results_file, current_results, previous_attempts, current_attempt)
        else:
            self.process_results(current_results, previous_attempts, current_attempt)

    def enqueue_retry(self, current_results_file, current_results, previous_attempts, current_attempt):
        pass

    def process_results(self, test_results, previous_attempts=None, current_attempt=1):
        """

        :type test_results: List[TestResult]
        :type previous_attempts: List[str]
        :type current_attempt: int
        """

        pass

    def upload_results(self, test_results):
        worker_count = 10
        q = Queue()

        print "Main thread starting workers"

        for i in range(worker_count):
            worker = UploadWorker(q, i, self.collection_uri, self.team_project, self.test_run_id, self.access_token)
            worker.daemon = True
            worker.start()

        batch_size = 1000

        print "Uploading {} results in batches of size {}".format(len(test_results), batch_size)

        batches = batch(test_results, batch_size)

        for b in batches:
            q.put(b)

        print "Main thread finished queueing batches"

        q.join()



def main():
    collection_uri, team_project, test_run_id, access_token, max_retries = process_args()

    print "Got args", collection_uri, team_project, test_run_id, access_token, max_retries

    uploader = TestResultUploader(collection_uri, team_project, test_run_id, access_token, max_retries)
    uploader.run()

    print "Main thread exiting"


if __name__ == '__main__':
    main()


